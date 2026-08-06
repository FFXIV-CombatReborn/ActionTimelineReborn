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
    /// </summary>
    public int MaxDelayMs = 600;

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
