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
    /// Startup permission block message for macOS capture permissions.
    /// </summary>
    public const string MacOSInputMonitoringStartupBlockMessage =
        "CrossMacro needs macOS Input Monitoring permission before capture and recording can run.\n\n" +
        "This startup check does not require event posting or Accessibility. Playback and text expansion will request event posting separately when needed.\n\n" +
        "Open System Settings now?";

    /// <summary>
    /// Legacy startup permission block message for permission checkers that only expose Accessibility.
    /// </summary>
    public const string MacOSAccessibilityStartupBlockMessage =
        "CrossMacro needs macOS Accessibility permission before this legacy permission gate can continue.\n\n" +
        "Modern macOS builds use Input Monitoring for capture and event posting for playback, but this checker exposes only Accessibility status.\n\n" +
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
