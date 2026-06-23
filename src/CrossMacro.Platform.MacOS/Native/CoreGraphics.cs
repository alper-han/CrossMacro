using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class CoreGraphics
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte PermissionAccessDelegate();

    private static readonly OptionalPermissionAccessFunction PreflightListenEventAccess = new("CGPreflightListenEventAccess");
    private static readonly OptionalPermissionAccessFunction RequestListenEventAccess = new("CGRequestListenEventAccess");
    private static readonly OptionalPermissionAccessFunction PreflightPostEventAccess = new("CGPreflightPostEventAccess");
    private static readonly OptionalPermissionAccessFunction RequestPostEventAccess = new("CGRequestPostEventAccess");
    private static readonly OptionalPermissionAccessFunction PreflightScreenCaptureAccess = new("CGPreflightScreenCaptureAccess");
    private static readonly OptionalPermissionAccessFunction RequestScreenCaptureAccess = new("CGRequestScreenCaptureAccess");

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr CGEventTapCallBack(
        IntPtr tapProxy,
        CGEventType type,
        IntPtr eventRef,
        IntPtr userInfo);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventTapCreate(
        CGEventTapLocation tap,
        CGEventTapPlacement place,
        CGEventTapOptions options,
        ulong eventsOfInterest,
        CGEventTapCallBack callback,
        IntPtr userInfo);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventPost(CGEventTapLocation tap, IntPtr eventRef);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    // These CoreGraphics ListenEvent/PostEvent TCC helpers are macOS 10.15+ era APIs.
    // Resolve them dynamically so older systems or unusual runtimes report unavailable instead of
    // failing with EntryPointNotFoundException when checking permission status.
    public static bool IsCGPreflightListenEventAccessAvailable()
    {
        return PreflightListenEventAccess.IsAvailable;
    }

    public static bool IsCGRequestListenEventAccessAvailable()
    {
        return RequestListenEventAccess.IsAvailable;
    }

    public static bool IsCGPreflightPostEventAccessAvailable()
    {
        return PreflightPostEventAccess.IsAvailable;
    }

    public static bool IsCGRequestPostEventAccessAvailable()
    {
        return RequestPostEventAccess.IsAvailable;
    }

    public static bool IsCGPreflightScreenCaptureAccessAvailable()
    {
        return PreflightScreenCaptureAccess.IsAvailable;
    }

    public static bool IsCGRequestScreenCaptureAccessAvailable()
    {
        return RequestScreenCaptureAccess.IsAvailable;
    }

    public static bool CGPreflightListenEventAccess()
    {
        return PreflightListenEventAccess.Invoke();
    }

    public static bool CGRequestListenEventAccess()
    {
        return RequestListenEventAccess.Invoke();
    }

    public static bool CGPreflightPostEventAccess()
    {
        return PreflightPostEventAccess.Invoke();
    }

    public static bool CGRequestPostEventAccess()
    {
        return RequestPostEventAccess.Invoke();
    }

    public static bool CGPreflightScreenCaptureAccess()
    {
        return PreflightScreenCaptureAccess.Invoke();
    }

    public static bool CGRequestScreenCaptureAccess()
    {
        return RequestScreenCaptureAccess.Invoke();
    }

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreateMouseEvent(
        IntPtr source,
        CGEventType mouseType,
        CGPoint mouseCursorPosition,
        CGMouseButton mouseButton);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreateScrollWheelEvent(
        IntPtr source,
        CGScrollEventUnit units,
        uint wheelCount,
        int wheel1);

    [DllImport(CoreGraphicsLib, EntryPoint = "CGEventCreateScrollWheelEvent")]
    public static extern IntPtr CGEventCreateScrollWheelEvent2(
        IntPtr source,
        CGScrollEventUnit units,
        uint wheelCount,
        int wheel1,
        int wheel2);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventSetFlags(IntPtr eventRef, CGEventFlags flags);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventSetType(IntPtr eventRef, CGEventType type);
    
    [DllImport(CoreGraphicsLib)]
    public static extern CGEventFlags CGEventGetFlags(IntPtr eventRef);

    [DllImport(CoreGraphicsLib)]
    public static extern long CGEventGetIntegerValueField(IntPtr eventRef, CGEventField field);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventSetIntegerValueField(IntPtr eventRef, CGEventField field, long value);
    
    [DllImport(CoreGraphicsLib)]
    public static extern CGPoint CGEventGetLocation(IntPtr eventRef);
    
    /// <summary>
    /// Gets the unicode string from a keyboard event
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventKeyboardGetUnicodeString(
        IntPtr eventRef,
        nuint maxStringLength,
        out nuint actualStringLength,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] ushort[] unicodeString);

    /// <summary>
    /// Sets the unicode string for a keyboard event (for typing characters)
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventKeyboardSetUnicodeString(
        IntPtr eventRef,
        nuint stringLength,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] ushort[] unicodeString);
    
    // Text Input Source (TIS) functions for keyboard layout
    private const string CarbonCoreLib = "/System/Library/Frameworks/CoreServices.framework/Frameworks/CarbonCore.framework/CarbonCore";
    private const string HIToolboxLib = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";
    
    [DllImport(HIToolboxLib)]
    public static extern IntPtr TISCopyCurrentKeyboardInputSource();
    
    [DllImport(HIToolboxLib)]
    public static extern IntPtr TISCopyCurrentKeyboardLayoutInputSource();
    
    [DllImport(HIToolboxLib)]
    public static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    [DllImport(HIToolboxLib)]
    public static extern byte LMGetKbdType();
    
    // Property key for Unicode keyboard layout data - loaded at runtime
    public static readonly IntPtr kTISPropertyUnicodeKeyLayoutData;
    
    static CoreGraphics()
    {
        try
        {
            IntPtr lib = NativeLibrary.Load(HIToolboxLib);
            IntPtr addr = NativeLibrary.GetExport(lib, "kTISPropertyUnicodeKeyLayoutData");
            kTISPropertyUnicodeKeyLayoutData = Marshal.ReadIntPtr(addr);
        }
        catch
        {
            kTISPropertyUnicodeKeyLayoutData = IntPtr.Zero;
        }
    }
    
    /// <summary>
    /// UCKeyTranslate - converts keycode to unicode character
    /// </summary>
    [DllImport(CarbonCoreLib)]
    public static extern int UCKeyTranslate(
        IntPtr keyLayoutPtr,
        ushort virtualKeyCode,
        ushort keyAction,
        uint modifierKeyState,
        uint keyboardType,
        uint keyTranslateOptions,
        ref uint deadKeyState,
        nuint maxStringLength,
        out nuint actualStringLength,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] ushort[] unicodeString);
    
    // UCKeyTranslate action types
    public const ushort kUCKeyActionDown = 0;
    public const ushort kUCKeyActionUp = 1;
    public const ushort kUCKeyActionAutoKey = 2;
    public const ushort kUCKeyActionDisplay = 3;
    
    // UCKeyTranslate options
    public const uint kUCKeyTranslateNoDeadKeysBit = 0;
    public const uint kUCKeyTranslateNoDeadKeysMask = 1;


    // Enums and Structs
    
    public enum CGEventTapLocation : uint
    {
        HIDEventTap = 0,
        SessionEventTap = 1,
        AnnotatedSessionEventTap = 2
    }

    public enum CGEventTapPlacement : uint
    {
        HeadInsertEventTap = 0,
        TailAppendEventTap = 1
    }
    
    public enum CGScrollEventUnit : uint
    {
        Pixel = 0,
        Line = 1
    }

    public enum CGEventTapOptions : uint
    {
        Default = 0x00000000,
        ListenOnly = 0x00000001
    }
    
    public enum CGEventType : uint
    {
        Null = 0,
        LeftMouseDown = 1,
        LeftMouseUp = 2,
        RightMouseDown = 3,
        RightMouseUp = 4,
        MouseMoved = 5,
        LeftMouseDragged = 6,
        RightMouseDragged = 7,
        KeyDown = 10,
        KeyUp = 11,
        FlagsChanged = 12,
        SystemDefined = 14,
        ScrollWheel = 22,
        TabletPointer = 23,
        TabletProximity = 24,
        OtherMouseDown = 25,
        OtherMouseUp = 26,
        OtherMouseDragged = 27,
        TapDisabledByTimeout = 0xFFFFFFFE,
        TapDisabledByUserInput = 0xFFFFFFFF
    }

    public enum CGMouseButton : uint
    {
        Left = 0,
        Right = 1,
        Center = 2
    }
    
    [Flags]
    public enum CGEventFlags : ulong
    {
        NonCoalesced = 0x0000000000000100,
        AlphaShift = 0x0000000000010000, // Caps Lock
        Shift = 0x0000000000020000,
        Control = 0x0000000000040000,
        Alternate = 0x0000000000080000, // Option
        Command = 0x0000000000100000,
        NumericPad = 0x0000000000200000,
        Help = 0x0000000000400000,
        SecondaryFn = 0x0000000000800000
    }
    
    public enum CGEventField : uint
    {
        MouseEventNumber = 0,
        MouseEventClickState = 1,
        MouseEventPressure = 2,
        MouseEventButtonNumber = 3,
        MouseEventDeltaX = 4,
        MouseEventDeltaY = 5,
        MouseEventInstantMouser = 6,
        MouseEventSubtype = 7,
        KeyboardEventAutorepeat = 8,
        KeyboardEventKeycode = 9,
        KeyboardEventKeyboardType = 10,
        EventSubtype = 83,
        EventData1 = 149,
        EventData2 = 150,
        EventSourceUnixProcessID = 41,
        EventSourceUserData = 42,
        ScrollWheelEventDeltaAxis1 = 11,
        ScrollWheelEventDeltaAxis2 = 12,
        ScrollWheelEventDeltaAxis3 = 13,
        ScrollWheelEventFixedPtDeltaAxis1 = 93,
        ScrollWheelEventFixedPtDeltaAxis2 = 94,
        ScrollWheelEventFixedPtDeltaAxis3 = 95,
        ScrollWheelEventPointDeltaAxis1 = 96,
        ScrollWheelEventPointDeltaAxis2 = 97,
        ScrollWheelEventPointDeltaAxis3 = 98,
        ScrollWheelEventInstantMouser = 14
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double width;
        public double height;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint origin;
        public CGSize size;
    }
    
    [DllImport(CoreGraphicsLib)]
    public static extern uint CGMainDisplayID();
    
    [DllImport(CoreGraphicsLib)]
    public static extern CGRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphicsLib)]
    public static extern CGError CGGetOnlineDisplayList(uint maxDisplays, [Out] uint[]? onlineDisplays, out uint displayCount);

    [DllImport(CoreGraphicsLib)]
    public static extern CGError CGGetActiveDisplayList(uint maxDisplays, [Out] uint[]? activeDisplays, out uint displayCount);

    [DllImport(CoreGraphicsLib)]
    public static extern CGError CGGetDisplaysWithRect(CGRect rect, uint maxDisplays, [Out] uint[]? displays, out uint displayCount);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGDisplayCreateImageForRect(uint display, CGRect rect);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGImageRelease(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGImageGetDataProvider(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGDataProviderCopyData(IntPtr provider);

    [DllImport(CoreGraphicsLib)]
    public static extern nuint CGImageGetWidth(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nuint CGImageGetHeight(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nuint CGImageGetBitsPerComponent(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nuint CGImageGetBitsPerPixel(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nuint CGImageGetBytesPerRow(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern CGBitmapInfo CGImageGetBitmapInfo(IntPtr image);

    public const uint kCGBitmapAlphaInfoMask = 0x1F;
    public const uint kCGBitmapByteOrderMask = 0x7000;
    public const uint kCGBitmapByteOrder32Little = 0x2000;
    public const uint kCGBitmapByteOrder32Big = 0x4000;

    public enum CGError : int
    {
        Success = 0
    }

    [Flags]
    public enum CGBitmapInfo : uint
    {
        AlphaPremultipliedLast = 1,
        AlphaPremultipliedFirst = 2,
        AlphaLast = 3,
        AlphaFirst = 4,
        AlphaNoneSkipLast = 5,
        AlphaNoneSkipFirst = 6,
        ByteOrder32Little = kCGBitmapByteOrder32Little,
        ByteOrder32Big = kCGBitmapByteOrder32Big
    }

    private sealed class OptionalPermissionAccessFunction
    {
        private readonly string _entryPoint;
        private readonly Lazy<PermissionAccessDelegate?> _function;

        internal OptionalPermissionAccessFunction(string entryPoint)
        {
            _entryPoint = entryPoint;
            _function = new Lazy<PermissionAccessDelegate?>(LoadFunction);
        }

        internal bool IsAvailable => _function.Value is not null;

        internal bool Invoke()
        {
            var function = _function.Value;
            return function is not null && function() != 0;
        }

        private PermissionAccessDelegate? LoadFunction()
        {
            if (!OperatingSystem.IsMacOS())
            {
                return null;
            }

            if (!NativeLibrary.TryLoad(CoreGraphicsLib, out var coreGraphics))
            {
                return null;
            }

            if (!NativeLibrary.TryGetExport(coreGraphics, _entryPoint, out var address))
            {
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<PermissionAccessDelegate>(address);
        }
    }
}
