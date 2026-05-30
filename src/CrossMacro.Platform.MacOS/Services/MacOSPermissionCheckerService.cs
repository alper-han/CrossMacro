using CrossMacro.Platform.MacOS.Helpers;
using CrossMacro.Platform.Abstractions;
using System.Runtime.Versioning;

namespace CrossMacro.Platform.MacOS.Services;

[SupportedOSPlatform("macos")]
public class MacOSPermissionCheckerService : IMacOSPermissionChecker
{
    private readonly Func<MacOSPermissionStatus> _getCurrentStatus;
    private readonly Func<bool> _isAccessibilityTrusted;
    private readonly Func<bool> _requestListenEventAccess;
    private readonly Func<bool> _requestPostEventAccess;

    public MacOSPermissionCheckerService()
        : this(
            MacOSPermissionChecker.GetCurrentStatus,
            MacOSPermissionChecker.IsAccessibilityTrusted,
            MacOSPermissionChecker.RequestListenEventAccess,
            MacOSPermissionChecker.RequestPostEventAccess)
    {
    }

    internal MacOSPermissionCheckerService(
        Func<MacOSPermissionStatus> getCurrentStatus,
        Func<bool> isAccessibilityTrusted,
        Func<bool>? requestListenEventAccess = null,
        Func<bool>? requestPostEventAccess = null)
    {
        _getCurrentStatus = getCurrentStatus;
        _isAccessibilityTrusted = isAccessibilityTrusted;
        _requestListenEventAccess = requestListenEventAccess ?? (() => false);
        _requestPostEventAccess = requestPostEventAccess ?? (() => false);
    }

    public bool IsSupported => true;
    public bool RequiresStartupPermissionGate => true;

    public MacOSPermissionStatus GetCurrentStatus()
    {
        return _getCurrentStatus();
    }

    public bool IsPermissionGranted(MacOSPermissionRequirement requirement)
    {
        return GetCurrentStatus().IsGranted(requirement);
    }

    public bool IsListenEventAccessGranted()
    {
        return IsPermissionGranted(MacOSPermissionRequirement.ListenEvent);
    }

    public bool IsPostEventAccessGranted()
    {
        return IsPermissionGranted(MacOSPermissionRequirement.PostEvent);
    }

    public bool RequestPermission(MacOSPermissionRequirement requirement)
    {
        return requirement switch
        {
            MacOSPermissionRequirement.ListenEvent => _requestListenEventAccess(),
            MacOSPermissionRequirement.PostEvent => _requestPostEventAccess(),
            MacOSPermissionRequirement.Accessibility => MacOSPermissionChecker.PromptAccessibilityPermission(),
            _ => false
        };
    }

    public bool RequestListenEventAccess()
    {
        return RequestPermission(MacOSPermissionRequirement.ListenEvent);
    }

    public bool RequestPostEventAccess()
    {
        return RequestPermission(MacOSPermissionRequirement.PostEvent);
    }

    public bool IsAccessibilityTrusted()
    {
        return _isAccessibilityTrusted();
    }

    public bool CheckUInputAccess()
    {
        // Not applicable on macOS
        return false;
    }

    public void OpenAccessibilitySettings()
    {
        MacOSPermissionChecker.OpenAccessibilitySettings();
    }

    public void OpenInputMonitoringSettings()
    {
        MacOSPermissionChecker.OpenInputMonitoringSettings();
    }
}
