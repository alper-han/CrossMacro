using System.Diagnostics;

namespace CrossMacro.Platform.MacOS.Services.Permissions;

internal sealed class MacOSPermissionNative : IMacOSPermissionNative
{
    public bool IsAccessibilityTrusted() => Accessibility.AXIsProcessTrusted();

    public bool PromptAccessibility() => Accessibility.AXIsProcessTrustedWithPrompt();

    public bool PreflightListenEvent() => CoreGraphics.CGPreflightListenEventAccess();

    public bool RequestListenEventWithIOHID() => IOKit.IOHIDRequestAccess(IOKit.IOHIDRequestType.ListenEvent);

    public bool RequestListenEventWithCoreGraphics() => CoreGraphics.CGRequestListenEventAccess();

    public bool PreflightPostEvent() => CoreGraphics.CGPreflightPostEventAccess();

    public bool RequestPostEvent() => CoreGraphics.CGRequestPostEventAccess();

    public bool PreflightScreenCapture() => CoreGraphics.CGPreflightScreenCaptureAccess();

    public bool RequestScreenCapture() => CoreGraphics.CGRequestScreenCaptureAccess();

    public void OpenPrivacySettings(string pane)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/open",
            Arguments = $"x-apple.systempreferences:com.apple.preference.security?{pane}",
            UseShellExecute = false,
        });
    }
}
