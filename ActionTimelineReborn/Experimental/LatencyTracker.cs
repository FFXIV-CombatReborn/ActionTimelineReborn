using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ActionTimelineReborn.Experimental;

/// <summary>
/// Captures the moment an action request leaves this client, so the timeline can be drawn
/// on the press clock instead of the packet-arrival clock.
///
/// Why this matters: every timestamp the plugin normally records is DateTime.Now inside a
/// server packet handler, i.e. press + network delay. BossMod's animation lock tweak, by
/// contrast, re-anchors the real animation lock to the press. The two clocks disagree by
/// the round trip, so with a high ping the drawn timeline lags what actually happened.
///
/// Rather than hooking ActionManager.UseActionLocation (which would mean a second detour on
/// a function BossMod already hooks, and a delegate signature that has to stay correct
/// across patches), this watches ActionManager.LastUsedActionSequence from the framework
/// tick. The sequence increments when the client sends the request, so a change means
/// "a request just went out". Accuracy is one frame, which is far below the delays this is
/// correcting, and nothing is hooked or written to.
/// </summary>
public sealed class LatencyTracker : IDisposable
{
    public static LatencyTracker? Instance { get; private set; }

    public static void Initialize()
    {
        Instance ??= new LatencyTracker();
    }

    private const int RingSize = 16;

    private readonly ushort[] _seq = new ushort[RingSize];
    private readonly DateTime[] _time = new DateTime[RingSize];
    private int _head;

    private ushort _lastSeenSequence;
    private bool _primed;
    private bool _wasEnabled;

    /// <summary>Smoothed request-&gt;response delay in seconds.</summary>
    public float AverageDelay { get; private set; }

    /// <summary>
    /// The offset a live compensated window should shift its clock by, glided toward
    /// <see cref="AverageDelay"/> a little each frame.
    ///
    /// Using the average directly makes the whole window lurch sideways whenever it
    /// updates: it only changes when an action resolves, so a delay spike moves every icon
    /// on screen by several pixels in a single frame. The average has to stay responsive
    /// because it also places effects that carry no request of their own, so this is a
    /// separate value that only ever moves smoothly.
    /// </summary>
    public float AnchorOffset { get; private set; }

    /// <summary>Seconds for the anchor to close ~63% of the distance to the average.</summary>
    private const float AnchorTimeConstant = 0.75f;

    private bool _anchorPrimed;

    /// <summary>Most recent matched delay in seconds.</summary>
    public float LastDelay { get; private set; }

    /// <summary>How many effects have been matched to a request since the feature was enabled.</summary>
    public int MatchedCount { get; private set; }

    /// <summary>How many effects fell back to the average (no usable source sequence).</summary>
    public int FallbackCount { get; private set; }

    private LatencyTracker()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Instance = null;
    }

    private static ExperimentalSettingsView Config => new(Plugin.Settings?.Experimental);

    /// <summary>
    /// Small read-only view so this class degrades safely if settings are not loaded yet.
    /// </summary>
    private readonly struct ExperimentalSettingsView(Configurations.ExperimentalSettings? s)
    {
        public bool Enable => s?.Enable ?? false;
        public float MaxDelay => (s?.MaxDelayMs ?? 600) / 1000f;
        public float Smoothing => Math.Clamp(s?.DelaySmoothing ?? 0.8f, 0f, 0.99f);
        public bool FallbackToAverage => s?.FallbackToAverage ?? true;
    }

    private unsafe void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        var enabled = Config.Enable;

        if (!enabled)
        {
            // Drop everything the moment the feature is switched off, so re-enabling later
            // cannot match an effect against a stale request from minutes ago.
            if (_wasEnabled) Reset();
            _wasEnabled = false;
            return;
        }

        _wasEnabled = true;

        UpdateAnchor(framework);

        if (!Player.Available)
        {
            _primed = false;
            return;
        }

        try
        {
            var manager = ActionManager.Instance();
            if (manager == null) return;

            var seq = manager->LastUsedActionSequence;

            if (!_primed)
            {
                // First tick after login/zone: adopt the current value without recording it,
                // otherwise we would stamp an old request with the current time.
                _lastSeenSequence = seq;
                _primed = true;
                return;
            }

            if (seq == _lastSeenSequence) return;

            _lastSeenSequence = seq;
            _seq[_head] = seq;
            _time[_head] = DateTime.Now;
            _head = (_head + 1) % RingSize;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "[ATR] LatencyTracker update failed");
        }
    }

    private void UpdateAnchor(Dalamud.Plugin.Services.IFramework framework)
    {
        if (MatchedCount == 0) return;

        // Snap on the first measurement, otherwise the window would visibly slide into
        // place over the first second after the feature is switched on.
        if (!_anchorPrimed)
        {
            AnchorOffset = AverageDelay;
            _anchorPrimed = true;
            return;
        }

        var dt = (float)framework.UpdateDelta.TotalSeconds;

        // Ignore a stalled or paused frame rather than lurching the whole window at once.
        if (dt <= 0f || dt > 1f) return;

        // Time-constant based rather than a flat per-frame fraction, so the glide takes the
        // same wall-clock time at 60fps and at 144fps.
        var k = 1f - MathF.Exp(-dt / AnchorTimeConstant);
        AnchorOffset += (AverageDelay - AnchorOffset) * k;
    }

    private void Reset()
    {
        Array.Clear(_seq);
        Array.Clear(_time);
        _head = 0;
        _primed = false;
        AverageDelay = 0;
        AnchorOffset = 0;
        _anchorPrimed = false;
        LastDelay = 0;
        MatchedCount = 0;
        FallbackCount = 0;
    }

    /// <summary>
    /// Resolve the press time for an effect that arrived at <paramref name="arrival"/>.
    /// Returns false if no usable estimate exists, in which case the caller must keep
    /// using the arrival time.
    /// </summary>
    public bool TryResolve(ushort sourceSequence, DateTime arrival, out DateTime requestTime, out float delaySeconds)
    {
        requestTime = arrival;
        delaySeconds = 0;

        var cfg = Config;
        if (!cfg.Enable) return false;

        if (sourceSequence != 0)
        {
            // Take the newest plausible entry, not the first in array order: the sequence
            // restarts on zone change and relog, so the ring can legitimately hold two
            // entries with the same number minutes apart.
            var best = default(DateTime);
            for (var i = 0; i < RingSize; i++)
            {
                if (_seq[i] != sourceSequence) continue;
                if (_time[i] == default) continue;

                var candidate = (arrival - _time[i]).TotalSeconds;
                if (candidate < 0 || candidate > cfg.MaxDelay) continue;
                if (_time[i] > best) best = _time[i];
            }

            if (best != default)
            {
                requestTime = best;
                delaySeconds = (float)(arrival - best).TotalSeconds;

                AverageDelay = MatchedCount == 0
                    ? delaySeconds
                    : delaySeconds * (1 - cfg.Smoothing) + AverageDelay * cfg.Smoothing;
                LastDelay = delaySeconds;
                MatchedCount++;
                return true;
            }
        }

        // Server-initiated effects (DoT ticks, auto attacks) carry no source sequence.
        if (!cfg.FallbackToAverage || MatchedCount == 0) return false;

        delaySeconds = AverageDelay;
        requestTime = arrival - TimeSpan.FromSeconds(AverageDelay);
        FallbackCount++;
        return true;
    }

    /// <summary>
    /// Resolve by recency rather than sequence, for the cast-start packet which carries no
    /// sequence of its own. Takes the newest request that is not implausibly old.
    /// </summary>
    public bool TryResolveByRecency(DateTime arrival, out DateTime requestTime, out float delaySeconds)
    {
        requestTime = arrival;
        delaySeconds = 0;

        var cfg = Config;
        if (!cfg.Enable) return false;

        var best = default(DateTime);
        for (var i = 0; i < RingSize; i++)
        {
            if (_time[i] == default) continue;
            var delay = (arrival - _time[i]).TotalSeconds;
            if (delay < 0 || delay > cfg.MaxDelay) continue;
            if (_time[i] > best) best = _time[i];
        }

        if (best != default)
        {
            requestTime = best;
            delaySeconds = (float)(arrival - best).TotalSeconds;
            return true;
        }

        if (!cfg.FallbackToAverage || MatchedCount == 0) return false;

        delaySeconds = AverageDelay;
        requestTime = arrival - TimeSpan.FromSeconds(AverageDelay);
        FallbackCount++;
        return true;
    }
}
