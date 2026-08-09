namespace CrossMacro.Core.Models;

/// <summary>Controls the trade-off between pointer fidelity and requested duration.</summary>
public enum MotionPlaybackMode
{
    /// <summary>Preserves recorded samples and may reduce effective playback speed.</summary>
    Precision,

    /// <summary>Resamples dense motion to a bounded rate while retaining duration.</summary>
    StrictSpeed,
}
