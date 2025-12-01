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
}
