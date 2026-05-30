using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class CoreFoundation
{
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, IntPtr order);

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern int CFRunLoopRunInMode(IntPtr mode, double seconds, bool returnAfterSourceHandled);
    
    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern void CFRunLoopRun();

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern void CFRunLoopStop(IntPtr rl);

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
    public static extern IntPtr CFRetain(IntPtr cf);
    
    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr cfData);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    public static readonly IntPtr kCFRunLoopCommonModes;
    public static readonly IntPtr kCFRunLoopDefaultMode;
    public static readonly IntPtr kCFBooleanTrue;

    static CoreFoundation()
    {
        // Load constants from native library
        IntPtr lib = NativeLibrary.Load(CoreFoundationLib);
        kCFRunLoopCommonModes = ReadIntPtr(lib, "kCFRunLoopCommonModes");
        kCFRunLoopDefaultMode = ReadIntPtr(lib, "kCFRunLoopDefaultMode");
        kCFBooleanTrue = ReadIntPtr(lib, "kCFBooleanTrue");
    }

    private static IntPtr ReadIntPtr(IntPtr lib, string name)
    {
        try
        {
            IntPtr addr = NativeLibrary.GetExport(lib, name);
            return Marshal.ReadIntPtr(addr);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
