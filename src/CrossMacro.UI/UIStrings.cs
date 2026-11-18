namespace CrossMacro.UI;

/// <summary>
/// Localized texts of the startup permission and screen-reading dialogs, resolved from the active language.
/// </summary>
public static class UIStrings
{
    public static string PermissionRequiredTitle => LocalizationService.Get("Permission_RequiredTitle");

    public static string MacOSInputMonitoringStartupBlockMessage => LocalizationService.Get("Permission_MacOSInputMonitoringStartupBlock");

    public static string MacOSAccessibilityStartupBlockMessage => LocalizationService.Get("Permission_MacOSAccessibilityStartupBlock");

    public static string MacOSPermissionApprovalRecheckMessage => LocalizationService.Get("Permission_MacOSApprovalRecheck");

    public static string MacOSInputMonitoringApprovalPendingMessage => LocalizationService.Get("Permission_MacOSInputMonitoringApprovalPending");

    public static string MacOSAccessibilityApprovalPendingMessage => LocalizationService.Get("Permission_MacOSAccessibilityApprovalPending");

    public static string MacOSScreenRecordingOptionalTitle => LocalizationService.Get("Permission_MacOSScreenRecordingOptionalTitle");

    public static string MacOSScreenRecordingOptionalMessage => LocalizationService.Get("Permission_MacOSScreenRecordingOptionalMessage");

    public static string PortalScreenReadingGuidanceTitle => LocalizationService.Get("ScreenReading_PortalGuidanceTitle");

    public static string PortalScreenReadingGuidanceMessage => LocalizationService.Get("ScreenReading_PortalGuidanceMessage");

    public static string OpenSettingsButton => LocalizationService.Get("Dialog_OpenSettings");

    public static string ContinueButton => LocalizationService.Get("Dialog_Continue");

    public static string EnableButton => LocalizationService.Get("Dialog_Enable");

    public static string NotNowButton => LocalizationService.Get("Dialog_NotNow");

    public static string ExitButton => LocalizationService.Get("Dialog_Exit");
}
