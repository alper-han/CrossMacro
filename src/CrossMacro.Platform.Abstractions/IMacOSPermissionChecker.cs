namespace CrossMacro.Platform.Abstractions;

public interface IMacOSPermissionChecker : IPermissionChecker
{
    public MacOSPermissionStatus GetCurrentStatus();
    public bool IsPermissionGranted(MacOSPermissionRequirement requirement);
    public bool IsListenEventAccessGranted();
    public bool IsListenEventListedOrGranted();
    public bool IsPostEventAccessGranted();
    public bool RequestPermission(MacOSPermissionRequirement requirement);
    public bool RequestListenEventAccess();
    public bool RequestPostEventAccess();
    public void OpenInputMonitoringSettings();
}
