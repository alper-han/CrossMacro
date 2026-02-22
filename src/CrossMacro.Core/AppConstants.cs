namespace CrossMacro.Core;

/// <summary>
/// Application-wide constants
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Display name of the application (for UI)
    /// </summary>
    public const string AppName = "CrossMacro";
    
    /// <summary>
    /// Identifier for app directories and config files (lowercase, no spaces)
    /// </summary>
    public const string AppIdentifier = "crossmacro";
    
    /// <summary>
    /// DBus service namespace for inter-process communication
    /// </summary>
    public const string DBusNamespace = "io.github.alper_han.crossmacro";
    
    /// <summary>
    /// Default recording hotkey
    /// </summary>
    public const string DefaultRecordingHotkey = "F8";
    
    /// <summary>
    /// Default playback hotkey
    /// </summary>
    public const string DefaultPlaybackHotkey = "F9";
    
    /// <summary>
    /// Default pause hotkey
    /// </summary>
    public const string DefaultPauseHotkey = "F10";
}
