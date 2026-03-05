namespace CrossMacro.UI;

/// <summary>
/// Centralized UI strings for dialogs and messages.
/// </summary>
public static class UIStrings
{
    /// <summary>
    /// Permission dialog title.
    /// </summary>
    public const string PermissionRequiredTitle = "Permission Required";
    
    /// <summary>
    /// Startup permission block message for macOS accessibility.
    /// </summary>
    public const string MacOSAccessibilityStartupBlockMessage =
        "CrossMacro cannot run without Accessibility permissions on macOS.\n\n" +
        "Global hotkeys, recording, playback and text expansion depend on system input access.\n\n" +
        "Open System Settings now?";
    
    /// <summary>
    /// Open settings button text.
    /// </summary>
    public const string OpenSettingsButton = "Open Settings";

    /// <summary>
    /// Exit button text.
    /// </summary>
    public const string ExitButton = "Exit";
}
