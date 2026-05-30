using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class Accessibility
{
    private const string ApplicationServicesLib = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [DllImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool AXIsProcessTrusted();

    [DllImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    public static bool AXIsProcessTrustedWithPrompt()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var promptOption = GetAXTrustedCheckOptionPrompt();
        if (promptOption == IntPtr.Zero || CoreFoundation.kCFBooleanTrue == IntPtr.Zero)
        {
            return AXIsProcessTrusted();
        }

        var options = CoreFoundation.CFDictionaryCreate(
            IntPtr.Zero,
            [promptOption],
            [CoreFoundation.kCFBooleanTrue],
            1,
            IntPtr.Zero,
            IntPtr.Zero);

        if (options == IntPtr.Zero)
        {
            return AXIsProcessTrusted();
        }

        try
        {
            return AXIsProcessTrustedWithOptions(options);
        }
        finally
        {
            CoreFoundation.CFRelease(options);
        }
    }

    private static IntPtr GetAXTrustedCheckOptionPrompt()
    {
        if (!NativeLibrary.TryLoad(ApplicationServicesLib, out var applicationServices))
        {
            return IntPtr.Zero;
        }

        if (!NativeLibrary.TryGetExport(applicationServices, "kAXTrustedCheckOptionPrompt", out var address))
        {
            return IntPtr.Zero;
        }

        return Marshal.ReadIntPtr(address);
    }
}
