namespace CrossMacro.Core.Models;

/// <summary>
/// Global application settings that persist across all profiles.
/// These settings are stored in global-settings.json at the config root.
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// Whether the system tray icon is enabled.
    /// </summary>
    public bool EnableTrayIcon { get; set; } = false;

    /// <summary>
    /// Whether the GUI should start minimized.
    /// </summary>
    public bool StartMinimized { get; set; } = false;

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
