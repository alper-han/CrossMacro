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
    /// Startup permission block message for the first macOS permission step.
    /// </summary>
    public const string MacOSInputMonitoringStartupBlockMessage =
        "CrossMacro needs two macOS permissions for normal macro use:\n\n" +
        "1. Input Monitoring lets CrossMacro read your keyboard and mouse for recording and shortcuts.\n" +
        "2. Accessibility lets CrossMacro play macros back by sending keyboard and mouse events.\n\n" +
        "CrossMacro will open the correct System Settings page and check again after you approve the permission. macOS requires your approval, but you do not need to add the app by hand.\n\n" +
        "Open System Settings now?";

    /// <summary>
    /// Startup permission block message for the macOS playback permission step.
    /// </summary>
    public const string MacOSAccessibilityStartupBlockMessage =
        "CrossMacro can read input now, but playback still needs macOS Accessibility permission.\n\n" +
        "Accessibility lets CrossMacro send keyboard and mouse events during playback. Input Monitoring alone only covers recording and shortcuts. CrossMacro will ask macOS to add the current app to the list and check again after you approve it.\n\n" +
        "Open System Settings now?";

    public const string MacOSPermissionApprovalRecheckMessage =
        "Approve CrossMacro in the System Settings page that just opened, then return here and click Continue.\n\n" +
        "CrossMacro will check the permission again and continue automatically when macOS reports it as granted.";

    public const string MacOSInputMonitoringApprovalPendingMessage =
        "macOS still does not report Input Monitoring permission for CrossMacro.\n\n" +
        "If you already approved it, quit and reopen CrossMacro so macOS can apply the new permission.";

    public const string MacOSAccessibilityApprovalPendingMessage =
        "macOS still does not report Accessibility permission for CrossMacro.\n\n" +
        "If you already approved it, quit and reopen CrossMacro so macOS can apply the new permission.";
    
    /// <summary>
    /// Open settings button text.
    /// </summary>
    public const string OpenSettingsButton = "Open Settings";

    public const string ContinueButton = "Continue";

    /// <summary>
    /// Exit button text.
    /// </summary>
    public const string ExitButton = "Exit";
}
