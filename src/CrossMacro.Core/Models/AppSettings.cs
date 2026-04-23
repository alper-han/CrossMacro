namespace CrossMacro.Core.Models;

/// <summary>
/// Application-wide settings
/// </summary>
public class AppSettings
{
    private double _playbackSpeed = PlaybackOptions.DefaultSpeedMultiplier;
    private int _loopDelayMs = PlaybackOptions.DefaultDelayMs;
    private int _loopDelayMinMs = PlaybackOptions.DefaultDelayMs;
    private int _loopDelayMaxMs = PlaybackOptions.DefaultDelayMs;

    /// <summary>
    /// Whether the system tray icon is enabled
    /// When disabled, closing the window will exit the application instead of minimizing to tray
    /// </summary>
    public bool EnableTrayIcon { get; set; } = false;

    /// <summary>
    /// Whether the GUI should start minimized.
    /// When tray icon support is available, startup hides to tray; otherwise the window starts minimized.
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    // Playback Settings

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
    /// Ignored when <see cref="UseRandomLoopDelay"/> is enabled.
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

    // Recording Settings

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
    /// Skip moving to 0,0 coordinate when recording starts (only applies when ForceRelativeCoordinates is true)
    /// When false, cursor moves to 0,0 at recording start for consistent baseline
    /// </summary>
    public bool SkipInitialZeroZero { get; set; } = false;

    // Text Expansion Settings

    /// <summary>
    /// Whether text expansion is enabled globally
    /// </summary>
    public bool EnableTextExpansion { get; set; } = false;

    // Update Settings

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckForUpdates { get; set; } = false;

    // Logging Settings

    /// <summary>
    /// Minimum log level for the application.
    /// Valid values: Debug, Information, Warning, Error
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Current UI Theme (Classic, Latte, Mocha, Dracula, Nord, Everforest, Gruvbox, Solarized, Crimson)
    /// </summary>
    public string Theme { get; set; } = "Mocha";

    /// <summary>
    /// Current UI language (en, tr, zh, ja, es, ar, fr, pt, ru).
    /// </summary>
    public string Language { get; set; } = "en";
}
