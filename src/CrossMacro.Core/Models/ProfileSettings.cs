namespace CrossMacro.Core.Models;

/// <summary>
/// Per-profile settings stored in profiles/{id}/settings.json.
/// These settings change when the user switches profiles.
/// </summary>
public class ProfileSettings
{
    /// <summary>
    /// Playback speed multiplier (1.0 = normal speed)
    /// </summary>
    public double PlaybackSpeed { get; set; } = PlaybackOptions.DefaultSpeedMultiplier;

    /// <summary>
    /// Whether to loop the macro
    /// </summary>
    public bool IsLooping { get; set; }

    /// <summary>
    /// Number of times to repeat the macro
    /// </summary>
    public int LoopCount { get; set; } = 1;

    /// <summary>
    /// Fixed delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    /// <summary>
    /// Whether to choose a random delay between loop repetitions.
    /// </summary>
    public bool UseRandomLoopDelay { get; set; }

    /// <summary>
    /// Minimum random delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMinMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    /// <summary>
    /// Maximum random delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMaxMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    /// <summary>Controls the trade-off between pointer fidelity and requested duration.</summary>
    public MotionPlaybackMode MotionMode { get; set; } = MotionPlaybackMode.Precision;

    /// <summary>Maximum injected pointer reports per second in StrictSpeed mode.</summary>
    public int StrictSpeedMotionEventsPerSecond { get; set; } = PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond;

    /// <summary>Precision output ceiling; playback slows down instead of dropping positions.</summary>
    public int PrecisionMotionEventsPerSecond { get; set; } = PlaybackOptions.DefaultPrecisionMotionEventsPerSecond;

    public double MaximumMotionErrorPixels { get; set; } = PlaybackOptions.DefaultMaximumMotionErrorPixels;

    public void Normalize()
    {
        PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(PlaybackSpeed);
        LoopDelayMs = PlaybackOptions.NormalizeDelayMs(LoopDelayMs);
        (LoopDelayMinMs, LoopDelayMaxMs) = PlaybackOptions.NormalizeDelayRange(LoopDelayMinMs, LoopDelayMaxMs);
        MotionMode = Enum.IsDefined(MotionMode) ? MotionMode : MotionPlaybackMode.Precision;
        StrictSpeedMotionEventsPerSecond = PlaybackOptions.NormalizeStrictSpeedMotionEventsPerSecond(StrictSpeedMotionEventsPerSecond);
        PrecisionMotionEventsPerSecond = PlaybackOptions.NormalizePrecisionMotionEventsPerSecond(PrecisionMotionEventsPerSecond);
        MaximumMotionErrorPixels = PlaybackOptions.NormalizeMaximumMotionErrorPixels(MaximumMotionErrorPixels);
    }

    /// <summary>
    /// Countdown seconds before playback starts
    /// </summary>
    public int CountdownSeconds { get; set; }

    /// <summary>
    /// Whether mouse recording is enabled
    /// </summary>
    public bool IsMouseRecordingEnabled { get; set; } = true;

    /// <summary>
    /// Whether keyboard recording is enabled
    /// </summary>
    public bool IsKeyboardRecordingEnabled { get; set; } = true;

    /// <summary>
    /// Force using relative coordinates even when absolute coordinates are supported
    /// </summary>
    public bool ForceRelativeCoordinates { get; set; }

    /// <summary>
    /// Skip moving to 0,0 coordinate when recording starts
    /// </summary>
    public bool SkipInitialZeroZero { get; set; }

    /// <summary>
    /// Whether text expansion is enabled globally
    /// </summary>
    public bool EnableTextExpansion { get; set; }

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckForUpdates { get; set; }
}
