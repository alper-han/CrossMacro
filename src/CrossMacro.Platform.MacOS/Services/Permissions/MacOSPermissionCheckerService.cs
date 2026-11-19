using System.Runtime.Versioning;
using CrossMacro.Platform.Abstractions.Permissions;

namespace CrossMacro.Platform.MacOS.Services.Permissions;

[SupportedOSPlatform("macos")]
internal sealed class MacOSPermissionCheckerService(IMacOSPermissionNative native)
    : IMacOSPermissionChecker, IMacOSScreenRecordingPermissionProbe
{
    private const string AccessibilityPane = "Privacy_Accessibility";
    private const string InputMonitoringPane = "Privacy_ListenEvent";
    private const string ScreenRecordingPane = "Privacy_ScreenCapture";

    private readonly IMacOSPermissionNative _native = native ?? throw new ArgumentNullException(nameof(native));

    public bool IsSupported => true;

    public bool RequiresStartupPermissionGate => true;

    /// <summary>The CoreGraphics preflight APIs ship with every supported macOS version.</summary>
    bool IMacOSScreenRecordingPermissionProbe.IsPreflightAvailable => true;

    bool IMacOSScreenRecordingPermissionProbe.IsGranted() => IsScreenRecordingAccessGranted();

    public MacOSPermissionStatus GetCurrentStatus()
    {
        return new MacOSPermissionStatus(
            ListenEventGranted: IsListenEventListedOrGranted(),
            PostEventGranted: IsPostEventAccessGranted(),
            AccessibilityGranted: IsAccessibilityTrusted(),
            ScreenRecordingGranted: IsScreenRecordingAccessGranted());
    }

    public bool IsPermissionGranted(MacOSPermissionRequirement requirement)
    {
        return requirement switch
        {
            MacOSPermissionRequirement.ListenEvent => IsListenEventListedOrGranted(),
            MacOSPermissionRequirement.PostEvent => IsPostEventAccessGranted(),
            MacOSPermissionRequirement.Accessibility => IsAccessibilityTrusted(),
            MacOSPermissionRequirement.ScreenRecording => IsScreenRecordingAccessGranted(),
            _ => false,
        };
    }

    public bool IsListenEventListedOrGranted() => _native.PreflightListenEvent();

    public bool IsPostEventAccessGranted() => _native.PreflightPostEvent();

    public bool IsScreenRecordingAccessGranted() => _native.PreflightScreenCapture();

    public bool IsAccessibilityTrusted() => _native.IsAccessibilityTrusted();

    public bool RequestPermission(MacOSPermissionRequirement requirement)
    {
        return requirement switch
        {
            MacOSPermissionRequirement.ListenEvent => RequestListenEventAccess(),
            MacOSPermissionRequirement.PostEvent => RequestPostEventAccess(),
            MacOSPermissionRequirement.Accessibility => _native.PromptAccessibility(),
            MacOSPermissionRequirement.ScreenRecording => RequestScreenRecordingAccess(),
            _ => false,
        };
    }

    /// <summary>Input Monitoring is asked for through IOHID first; CoreGraphics is only consulted when that did not grant it.</summary>
    public bool RequestListenEventAccess() =>
        _native.RequestListenEventWithIOHID() || _native.RequestListenEventWithCoreGraphics();

    public bool RequestPostEventAccess() => _native.RequestPostEvent();

    public bool RequestScreenRecordingAccess() => _native.RequestScreenCapture();

    public bool CheckUInputAccess() => false;

    public ValueTask<bool> CheckUInputAccessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }

    public void OpenAccessibilitySettings() => _native.OpenPrivacySettings(AccessibilityPane);

    public void OpenInputMonitoringSettings() => _native.OpenPrivacySettings(InputMonitoringPane);

    public void OpenScreenRecordingSettings() => _native.OpenPrivacySettings(ScreenRecordingPane);
}
