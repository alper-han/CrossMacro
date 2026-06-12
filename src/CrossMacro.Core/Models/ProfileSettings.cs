namespace CrossMacro.Core.Models;

/// <summary>
/// Per-profile settings stored in profiles/{id}/settings.json.
/// These settings change when the user switches profiles.
/// </summary>
public class ProfileSettings
{
    private double _playbackSpeed = PlaybackOptions.DefaultSpeedMultiplier;
    private int _loopDelayMs = PlaybackOptions.DefaultDelayMs;
    private int _loopDelayMinMs = PlaybackOptions.DefaultDelayMs;
    private int _loopDelayMaxMs = PlaybackOptions.DefaultDelayMs;

    /// <summary>
    /// Playback speed multiplier (1.0 = normal speed)
    /// </summary>
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(value);
    }

    /// <summary>
    /// Whether to loop the macro
    /// </summary>
    public bool IsLooping { get; set; } = false;

    /// <summary>
    /// Number of times to repeat the macro
    /// </summary>
    public int LoopCount { get; set; } = 1;

    /// <summary>
    /// Fixed delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMs
    {
        get => _loopDelayMs;
        set => _loopDelayMs = PlaybackOptions.NormalizeDelayMs(value);
    }

    /// <summary>
    /// Whether to choose a random delay between loop repetitions.
    /// </summary>
    public bool UseRandomLoopDelay { get; set; } = false;

    /// <summary>
    /// Minimum random delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMinMs
    {
        get => _loopDelayMinMs;
        set
        {
            var normalized = PlaybackOptions.NormalizeDelayMs(value);
            _loopDelayMinMs = normalized;
            if (_loopDelayMaxMs < normalized)
            {
                _loopDelayMaxMs = normalized;
            }
        }
    }

    /// <summary>
    /// Maximum random delay between loop repetitions in milliseconds.
    /// </summary>
    public int LoopDelayMaxMs
    {
        get => _loopDelayMaxMs;
        set
        {
            var normalized = PlaybackOptions.NormalizeDelayMs(value);
            _loopDelayMaxMs = System.Math.Max(normalized, _loopDelayMinMs);
        }
    }

    /// <summary>
    /// Countdown seconds before playback starts
    /// </summary>
    public int CountdownSeconds { get; set; } = 0;

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
    public bool ForceRelativeCoordinates { get; set; } = false;

    /// <summary>
    /// Skip moving to 0,0 coordinate when recording starts
    /// </summary>
    public bool SkipInitialZeroZero { get; set; } = false;

    /// <summary>
    /// Whether text expansion is enabled globally
    /// </summary>
    public bool EnableTextExpansion { get; set; } = false;

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckForUpdates { get; set; } = false;
}
