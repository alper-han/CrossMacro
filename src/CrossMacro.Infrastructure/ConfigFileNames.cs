namespace CrossMacro.Infrastructure;

/// <summary>
/// Centralized configuration file names.
/// All JSON config file names should be defined here.
/// </summary>
public static class ConfigFileNames
{
    /// <summary>
    /// Application settings file.
    /// </summary>
    public const string Settings = "settings.json";
    
    /// <summary>
    /// Scheduled tasks file.
    /// </summary>
    public const string Schedules = "schedules.json";
    
    /// <summary>
    /// Keyboard shortcuts file.
    /// </summary>
    public const string Shortcuts = "shortcuts.json";
    
    /// <summary>
    /// Text expansions file.
    /// </summary>
    public const string TextExpansions = "text-expansions.json";
    
    /// <summary>
    /// Hotkey configuration file.
    /// </summary>
    public const string Hotkeys = "hotkeys.json";

    /// <summary>
    /// Profile registry file tracking available profiles and active profile.
    /// </summary>
    public const string ProfileRegistry = "profile-registry.json";

    /// <summary>
    /// Global settings file (theme, language, log level, tray — shared across profiles).
    /// </summary>
    public const string GlobalSettings = "global-settings.json";

    /// <summary>
    /// Subdirectory containing per-profile configuration folders.
    /// </summary>
    public const string ProfilesDirectory = "profiles";
}
