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
}
