namespace CrossMacro.Core.Models;

/// <summary>
/// Application-wide settings
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether the system tray icon is enabled
    /// When disabled, closing the window will exit the application instead of minimizing to tray
    /// </summary>
    public bool EnableTrayIcon { get; set; } = true;
    
    // Playback Settings
    
    /// <summary>
    /// Playback speed multiplier (1.0 = normal speed)
    /// </summary>
    public double PlaybackSpeed { get; set; } = 1.0;
    
    /// <summary>
    /// Whether to loop the macro
    /// </summary>
    public bool IsLooping { get; set; } = false;
    
    /// <summary>
    /// Number of times to repeat the macro
    /// </summary>
    public int LoopCount { get; set; } = 1;
    
    /// <summary>
    /// Delay between loop repetitions in milliseconds
    /// </summary>
    public int LoopDelayMs { get; set; } = 0;
    
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

}

