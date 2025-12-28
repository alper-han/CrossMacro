using CrossMacro.Platform.MacOS.Native;
using System.Diagnostics;

namespace CrossMacro.Platform.MacOS.Helpers;

public static class MacOSPermissionChecker
{
    public static bool IsAccessibilityTrusted()
    {
        return Accessibility.AXIsProcessTrusted();
    }
    
    public static bool PromptAccessibilityPermission()
    {
        // No native prompt support without CFDictionary construction.
        // The UI handles showing a dialog if this returns false.
        return IsAccessibilityTrusted();
    }
    
    public static void OpenAccessibilitySettings()
    {
        // Opens System Settings directly to Accessibility privacy section
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
            UseShellExecute = false
        });
    }
}
