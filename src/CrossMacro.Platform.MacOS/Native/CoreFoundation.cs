
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
    public static partial nint CFArrayGetCount(IntPtr array);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [LibraryImport(CoreFoundationLib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

    [LibraryImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool CFStringGetCString(IntPtr value, byte[] buffer, nint bufferSize, uint encoding);

    [LibraryImport(CoreFoundationLib)]
    public static partial nint CFGetTypeID(IntPtr value);

    [LibraryImport(CoreFoundationLib)]
    public static partial nint CFStringGetTypeID();

    [LibraryImport(CoreFoundationLib)]
    public static partial nint CFStringGetLength(IntPtr value);

    [LibraryImport(CoreFoundationLib)]
    public static partial nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool CFEqual(IntPtr left, IntPtr right);

    [LibraryImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool CFNumberGetValue(IntPtr value, int numberType, out int result);

    [LibraryImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool CFBooleanGetValue(IntPtr value);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint numValues,
        IntPtr keyCallBacks,
        IntPtr valueCallBacks);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CFNotificationCallback(IntPtr center, IntPtr observer, IntPtr name, IntPtr notificationObject, IntPtr userInfo);

    [LibraryImport(CoreFoundationLib)]
    public static partial IntPtr CFNotificationCenterGetDistributedCenter();

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFNotificationCenterAddObserver(
        IntPtr center,
        IntPtr observer,
        nint callback,
        IntPtr name,
        IntPtr notificationObject,
        nint suspensionBehavior);

    [LibraryImport(CoreFoundationLib)]
    public static partial void CFNotificationCenterRemoveObserver(
        IntPtr center,
        IntPtr observer,
        IntPtr name,
        IntPtr notificationObject);

    public static readonly IntPtr kCFRunLoopCommonModes = NativeSymbols.ReadPointer(CoreFoundationLib, "kCFRunLoopCommonModes");
    public static readonly IntPtr kCFRunLoopDefaultMode = NativeSymbols.ReadPointer(CoreFoundationLib, "kCFRunLoopDefaultMode");
    public static readonly IntPtr kCFBooleanTrue = NativeSymbols.ReadPointer(CoreFoundationLib, "kCFBooleanTrue");
    public static readonly IntPtr kCFBooleanFalse = NativeSymbols.ReadPointer(CoreFoundationLib, "kCFBooleanFalse");

    public const uint kCFStringEncodingUtf8 = 0x08000100;
    public const int kCFNumberSInt32Type = 3;
}
