
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class Accessibility
{
    private const string ApplicationServicesLib = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [LibraryImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool AXIsProcessTrusted();

    [LibraryImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool AXIsProcessTrustedWithOptions(IntPtr options);

    [LibraryImport(ApplicationServicesLib)]
    public static partial IntPtr AXUIElementCreateSystemWide();

    [LibraryImport(ApplicationServicesLib)]
    public static partial IntPtr AXUIElementCreateApplication(int pid);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementSetAttributeValue(IntPtr element, IntPtr attribute, IntPtr value);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementPerformAction(IntPtr element, IntPtr action);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementGetPid(IntPtr element, out int pid);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementIsAttributeSettable(
        IntPtr element,
        IntPtr attribute,
        [MarshalAs(UnmanagedType.I1)] out bool settable);

    [LibraryImport(ApplicationServicesLib)]
    public static partial int AXUIElementSetMessagingTimeout(IntPtr element, float timeoutInSeconds);

    [LibraryImport(ApplicationServicesLib)]
    public static partial IntPtr AXValueCreate(int type, IntPtr value);

    [LibraryImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool AXValueGetValue(IntPtr value, int type, out CoreGraphics.CGPoint result);

    [LibraryImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool AXValueGetValue(IntPtr value, int type, out CoreGraphics.CGSize result);

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
