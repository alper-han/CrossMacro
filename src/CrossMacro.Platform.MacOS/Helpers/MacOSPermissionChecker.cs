using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.Abstractions;
using System.Diagnostics;

namespace CrossMacro.Platform.MacOS.Helpers;

public static class MacOSPermissionChecker
{
    public static bool IsAccessibilityTrusted()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            return Accessibility.AXIsProcessTrusted();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static MacOSPermissionStatus GetCurrentStatus()
    {
        var listenEventApiAvailable = IsListenEventPreflightApiAvailable();
        var postEventApiAvailable = IsPostEventPreflightApiAvailable();

        return new MacOSPermissionStatus(
            ListenEventGranted: listenEventApiAvailable && PreflightListenEventAccess(),
            PostEventGranted: postEventApiAvailable && PreflightPostEventAccess(),
            AccessibilityGranted: IsAccessibilityTrusted(),
            ListenEventApiAvailable: listenEventApiAvailable,
            PostEventApiAvailable: postEventApiAvailable);
    }

    public static bool IsListenEventAccessGranted()
    {
        return GetCurrentStatus().IsGranted(MacOSPermissionRequirement.ListenEvent);
    }

    public static bool IsPostEventAccessGranted()
    {
        return GetCurrentStatus().IsGranted(MacOSPermissionRequirement.PostEvent);
    }

    public static bool RequestListenEventAccess()
    {
        return RequestPermissionAccess(
            isApiAvailable: IsListenEventRequestApiAvailable,
            requestAccess: CoreGraphics.CGRequestListenEventAccess);
    }

    public static bool RequestPostEventAccess()
    {
        return RequestPermissionAccess(
            isApiAvailable: IsPostEventRequestApiAvailable,
            requestAccess: CoreGraphics.CGRequestPostEventAccess);
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

    public static void OpenInputMonitoringSettings()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "open",
            Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent",
            UseShellExecute = false
        });
    }

    private static bool IsListenEventPreflightApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGPreflightListenEventAccessAvailable();
    }

    private static bool IsListenEventRequestApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGRequestListenEventAccessAvailable();
    }

    private static bool IsPostEventPreflightApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGPreflightPostEventAccessAvailable();
    }

    private static bool IsPostEventRequestApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGRequestPostEventAccessAvailable();
    }

    private static bool PreflightListenEventAccess()
    {
        try
        {
            return CoreGraphics.CGPreflightListenEventAccess();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool PreflightPostEventAccess()
    {
        try
        {
            return CoreGraphics.CGPreflightPostEventAccess();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool RequestPermissionAccess(
        Func<bool> isApiAvailable,
        Func<bool> requestAccess)
    {
        if (!OperatingSystem.IsMacOS() || !isApiAvailable())
        {
            return false;
        }

        try
        {
            return requestAccess();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
