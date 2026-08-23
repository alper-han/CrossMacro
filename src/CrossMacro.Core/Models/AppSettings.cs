namespace CrossMacro.Core.Models;

/// <summary>
/// Application-wide settings
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Local MCP capability policy. This is global and applies to every MCP session.
    /// </summary>
    public McpSecuritySettings McpSecurity { get; set; } = new();

    /// <summary>
    /// Whether the system tray icon is enabled
    /// When disabled, closing the window will exit the application instead of minimizing to tray
    /// </summary>
    public bool EnableTrayIcon { get; set; }

    /// <summary>
    /// Whether the GUI should start minimized.
    /// When tray icon support is available, startup hides to tray; otherwise the window starts minimized.
    /// </summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// Whether the user chose not to see the warning for fast loop playback again.
    /// </summary>
    public bool SuppressFastLoopWarning { get; set; }

    /// <summary>
    /// Whether the user completed the optional macOS Screen Recording onboarding choice.
    /// </summary>
    public bool MacOSScreenRecordingOnboardingCompleted { get; set; }

    // Playback Settings

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
    /// Ignored when <see cref="UseRandomLoopDelay"/> is enabled.
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

    /// <summary>Maximum pixel error allowed when StrictSpeed simplifies a trajectory.</summary>
    public double MaximumMotionErrorPixels { get; set; } = PlaybackOptions.DefaultMaximumMotionErrorPixels;

    public void Normalize()
    {
        McpSecurity ??= new McpSecuritySettings();
        McpSecurity.Normalize();
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
    public bool ForceRelativeCoordinates { get; set; }

    /// <summary>
    /// Record forced-relative mouse movement as logical desktop-pixel deltas instead of raw device deltas.
    /// </summary>
    public bool UseLogicalRelativeCoordinates { get; set; }

    /// <summary>
    /// Skip moving to 0,0 coordinate when recording starts (only applies when ForceRelativeCoordinates is true)
    /// When false, cursor moves to 0,0 at recording start for consistent baseline
    /// </summary>
    public bool SkipInitialZeroZero { get; set; }

    // Text Expansion Settings

    /// <summary>
    /// Whether text expansion is enabled globally
    /// </summary>
    public bool EnableTextExpansion { get; set; }

    // Update Settings

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckForUpdates { get; set; }

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
