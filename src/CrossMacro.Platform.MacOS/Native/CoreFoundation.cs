
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class CoreFoundation
{
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, IntPtr order);

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFRunLoopGetCurrent();

    [LibraryImport(CoreFoundationLib)]
    public static partial int CFRunLoopRunInMode(IntPtr mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFRunLoopRun();

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFRunLoopStop(IntPtr rl);

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFRelease(IntPtr cf);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFRetain(IntPtr cf);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFDataGetBytePtr(IntPtr cfData);

    [LibraryImport(CoreFoundationLib)]
    public static partial nint CFDataGetLength(IntPtr cfData);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    private static readonly IntPtr _lib = TryLoadLib(CoreFoundationLib);
    public static readonly IntPtr kCFRunLoopCommonModes = ReadIntPtr(_lib, "kCFRunLoopCommonModes");
    public static readonly IntPtr kCFRunLoopDefaultMode = ReadIntPtr(_lib, "kCFRunLoopDefaultMode");
    public static readonly IntPtr kCFBooleanTrue = ReadIntPtr(_lib, "kCFBooleanTrue");

    private static IntPtr TryLoadLib(string libPath)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return IntPtr.Zero;
        }

        return NativeLibrary.TryLoad(libPath, out var lib) ? lib : IntPtr.Zero;
    }

    private static IntPtr ReadIntPtr(IntPtr lib, string name)
    {
        if (lib == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr addr = NativeLibrary.GetExport(lib, name);
            return Marshal.ReadIntPtr(addr);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return IntPtr.Zero;
        }
    }
}
