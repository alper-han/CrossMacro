namespace CrossMacro.Platform.Abstractions;

public interface IMacOSPermissionChecker : IPermissionChecker
{
    MacOSPermissionStatus GetCurrentStatus();
    bool IsPermissionGranted(MacOSPermissionRequirement requirement);
    bool IsListenEventAccessGranted();
    bool IsListenEventListedOrGranted();
    bool IsPostEventAccessGranted();
    bool RequestPermission(MacOSPermissionRequirement requirement);
    bool RequestListenEventAccess();
    bool RequestPostEventAccess();
    void OpenInputMonitoringSettings();
}
