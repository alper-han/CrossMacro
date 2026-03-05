using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Helpers;
using System.Runtime.Versioning;

namespace CrossMacro.Platform.MacOS.Services;

[SupportedOSPlatform("macos")]
public class MacOSPermissionCheckerService : IPermissionChecker
{
    public bool IsSupported => true;
    public bool RequiresStartupPermissionGate => true;

    public bool IsAccessibilityTrusted()
    {
        return MacOSPermissionChecker.IsAccessibilityTrusted();
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
}
