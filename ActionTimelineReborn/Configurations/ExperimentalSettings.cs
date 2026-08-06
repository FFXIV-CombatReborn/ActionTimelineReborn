using System.Numerics;

namespace ActionTimelineReborn.Configurations;

/// <summary>
/// Settings for the experimental latency-compensation feature.
/// Everything here is additive: with <see cref="Enable"/> false the plugin behaves
/// exactly as it did before this feature existed.
/// </summary>
[Serializable]
public class ExperimentalSettings
{
    /// <summary>
    /// Master switch. While false no request timestamps are captured at all and every
    /// timeline draws from the original packet-arrival clock.
    /// </summary>
    public bool Enable = false;

    /// <summary>
    /// Measured request->response delays above this are treated as a bad match
    /// (sequence reuse, a stall, a loading screen) and discarded.
    /// Raid latency spikes well past a nominal ping, so this has to sit comfortably
    /// above the worst case or the spikiest actions are the ones that get thrown away.
    /// </summary>
    public int MaxDelayMs = 1200;

    /// <summary>
    /// Exponential smoothing factor for the running delay average, matching the
    /// approach BossMod uses for its own estimate. Higher = smoother/slower.
    /// </summary>
    public float DelaySmoothing = 0.8f;

    /// <summary>
    /// Use the smoothed average delay for effects that carry no source sequence
    /// (DoT ticks, auto attacks, anything the server initiated).
    /// </summary>
    public bool FallbackToAverage = true;

    /// <summary>
    /// Shift a live compensated window's clock back by the average delay, so the newest
    /// action still appears at the leading edge instead of popping in already displaced.
    ///
    /// Drawing on the press clock is truthful but feels laggy: we only learn an action
    /// happened when its packet returns, by which point the press is already the delay in
    /// the past, so the icon materialises away from the edge. What actually removes the
    /// false gaps is exact spacing *between* actions, and a constant shift preserves that
    /// completely. This trades absolute wall-clock alignment, which buys nothing, for the
    /// responsive feel of the original.
    /// </summary>
    public bool AnchorLatestToEdge = true;

    /// <summary>
    /// Draw the measured network delay for each action as its own thin band, so the
    /// ping is still visible without being scored as a mistake.
    /// </summary>
    public bool ShowLatencyBand = false;

    public Vector4 LatencyBandColor = new(0.35f, 0.65f, 1f, 0.55f);

    /// <summary>
    /// Show the live measured delay readout in the experimental settings tab.
    /// </summary>
    public bool ShowDebugReadout = true;
}
