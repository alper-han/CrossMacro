using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class CoreGraphics
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

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
    public static extern bool CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventPost(CGEventTapLocation tap, IntPtr eventRef);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

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
        [Out] char[] unicodeString);

    /// <summary>
    /// Sets the unicode string for a keyboard event (for typing characters)
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern void CGEventKeyboardSetUnicodeString(
        IntPtr eventRef,
        nuint stringLength,
        char[] unicodeString);
    
    // Text Input Source (TIS) functions for keyboard layout
    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";
    
    [DllImport(CarbonLib)]
    public static extern IntPtr TISCopyCurrentKeyboardInputSource();
    
    [DllImport(CarbonLib)]
    public static extern IntPtr TISCopyCurrentKeyboardLayoutInputSource();
    
    [DllImport(CarbonLib)]
    public static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);
    
    // Property key for Unicode keyboard layout data - loaded at runtime
    public static readonly IntPtr kTISPropertyUnicodeKeyLayoutData;
    
    static CoreGraphics()
    {
        try
        {
            IntPtr lib = NativeLibrary.Load(CarbonLib);
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
    [DllImport(CarbonLib)]
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
        [Out] char[] unicodeString);
    
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
}
