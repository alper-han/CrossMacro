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
        var listenEventApiAvailable = IsCoreGraphicsListenEventPreflightApiAvailable();
        var postEventApiAvailable = IsPostEventPreflightApiAvailable();

        return new MacOSPermissionStatus(
            ListenEventGranted: listenEventApiAvailable && PreflightListenEventListedOrGranted(),
            PostEventGranted: postEventApiAvailable && PreflightPostEventAccess(),
            AccessibilityGranted: IsAccessibilityTrusted(),
            ListenEventApiAvailable: listenEventApiAvailable,
            PostEventApiAvailable: postEventApiAvailable);
    }

    public static bool IsListenEventAccessGranted()
    {
        return IsListenEventListedOrGranted();
    }

    public static bool IsListenEventListedOrGranted()
    {
        return PreflightListenEventListedOrGranted();
    }

    public static bool IsPostEventAccessGranted()
    {
        return IsPostEventPreflightApiAvailable() && PreflightPostEventAccess();
    }

    public static bool RequestListenEventAccess()
    {
        var ioHidRequested = RequestPermissionAccess(
            isApiAvailable: IsIOHIDListenEventRequestApiAvailable,
            requestAccess: IOKit.RequestListenEventAccess);
        var coreGraphicsRequested = RequestPermissionAccess(
            isApiAvailable: IsListenEventRequestApiAvailable,
            requestAccess: CoreGraphics.CGRequestListenEventAccess);

        return ioHidRequested || coreGraphicsRequested;
    }

    public static bool RequestPostEventAccess()
    {
        return RequestPermissionAccess(
            isApiAvailable: IsPostEventRequestApiAvailable,
            requestAccess: CoreGraphics.CGRequestPostEventAccess)
            || PromptAccessibilityPermission();
    }
    
    public static bool PromptAccessibilityPermission()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            return Accessibility.AXIsProcessTrustedWithPrompt();
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

    private static bool IsCoreGraphicsListenEventPreflightApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGPreflightListenEventAccessAvailable();
    }

    private static bool IsListenEventRequestApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15)
            && CoreGraphics.IsCGRequestListenEventAccessAvailable();
    }

    private static bool IsIOHIDListenEventRequestApiAvailable()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(10, 15);
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

    private static bool PreflightCoreGraphicsListenEventAccess()
    {
        return CoreGraphics.CGPreflightListenEventAccess();
    }

    private static bool PreflightListenEventListedOrGranted()
    {
        return CheckPermissionAccess(
            isApiAvailable: IsCoreGraphicsListenEventPreflightApiAvailable,
            checkAccess: PreflightCoreGraphicsListenEventAccess);
    }

    private static bool PreflightPostEventAccess()
    {
        return CheckPermissionAccess(
            isApiAvailable: () => OperatingSystem.IsMacOSVersionAtLeast(10, 15)
                && CoreGraphics.IsCGPreflightPostEventAccessAvailable(),
            checkAccess: CoreGraphics.CGPreflightPostEventAccess);
    }

    private static bool CheckPermissionAccess(Func<bool> isApiAvailable, Func<bool> checkAccess)
    {
        if (!OperatingSystem.IsMacOS() || !isApiAvailable())
        {
            return false;
        }

        try
        {
            return checkAccess();
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
