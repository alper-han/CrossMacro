
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class CoreGraphics
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

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGEventTapCreate(
        CGEventTapLocation tap,
        CGEventTapPlacement place,
        CGEventTapOptions options,
        ulong eventsOfInterest,
        nint callback,
        IntPtr userInfo);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventPost(CGEventTapLocation tap, IntPtr eventRef);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGEventCreate(IntPtr source);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

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

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGEventCreateMouseEvent(
        IntPtr source,
        CGEventType mouseType,
        CGPoint mouseCursorPosition,
        CGMouseButton mouseButton);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGEventCreateScrollWheelEvent(
        IntPtr source,
        CGScrollEventUnit units,
        uint wheelCount,
        int wheel1);

    [LibraryImport(CoreGraphicsLib, EntryPoint = "CGEventCreateScrollWheelEvent")]
    public static partial IntPtr CGEventCreateScrollWheelEvent2(
        IntPtr source,
        CGScrollEventUnit units,
        uint wheelCount,
        int wheel1,
        int wheel2);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventSetFlags(IntPtr eventRef, CGEventModifiers flags);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventSetType(IntPtr eventRef, CGEventType type);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGEventModifiers CGEventGetFlags(IntPtr eventRef);

    [LibraryImport(CoreGraphicsLib)]
    public static partial ulong CGEventGetTimestamp(IntPtr eventRef);

    [LibraryImport(CoreGraphicsLib)]
    public static partial long CGEventGetIntegerValueField(IntPtr eventRef, CGEventField field);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventSetIntegerValueField(IntPtr eventRef, CGEventField field, long value);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGPoint CGEventGetLocation(IntPtr eventRef);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [LibraryImport(CoreGraphicsLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionary, out CGRect rect);

    /// <summary>
    /// Gets the unicode string from a keyboard event
    /// </summary>
    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventKeyboardGetUnicodeString(
        IntPtr eventRef,
        nuint maxStringLength,
        out nuint actualStringLength,
        [Out] ushort[] unicodeString);

    /// <summary>
    /// Sets the unicode string for a keyboard event (for typing characters)
    /// </summary>
    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGEventKeyboardSetUnicodeString(
        IntPtr eventRef,
        nuint stringLength,
        ushort[] unicodeString);

    // Text Input Source (TIS) functions for keyboard layout
    private const string CarbonCoreLib = "/System/Library/Frameworks/CoreServices.framework/Frameworks/CarbonCore.framework/CarbonCore";
    private const string HIToolboxLib = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";

    [LibraryImport(HIToolboxLib)]
    public static partial IntPtr TISCopyCurrentKeyboardInputSource();

    [LibraryImport(HIToolboxLib)]
    public static partial IntPtr TISCopyCurrentKeyboardLayoutInputSource();

    [LibraryImport(HIToolboxLib)]
    public static partial IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    [LibraryImport(HIToolboxLib)]
    public static partial byte LMGetKbdType();

    // Property key for Unicode keyboard layout data - loaded at runtime
    public static readonly IntPtr kTISPropertyUnicodeKeyLayoutData = LoadHIToolboxConstant("kTISPropertyUnicodeKeyLayoutData");

    private static IntPtr LoadHIToolboxConstant(string name)
    {
        try
        {
            IntPtr lib = NativeLibrary.Load(HIToolboxLib);
            IntPtr addr = NativeLibrary.GetExport(lib, name);
            return Marshal.ReadIntPtr(addr);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// UCKeyTranslate - converts keycode to unicode character
    /// </summary>
    [LibraryImport(CarbonCoreLib)]
    public static partial int UCKeyTranslate(
        IntPtr keyLayoutPtr,
        ushort virtualKeyCode,
        ushort keyAction,
        uint modifierKeyState,
        uint keyboardType,
        uint keyTranslateOptions,
        ref uint deadKeyState,
        nuint maxStringLength,
        out nuint actualStringLength,
        [Out] ushort[] unicodeString);

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
        AnnotatedSessionEventTap = 2,
    }

    public enum CGEventTapPlacement : uint
    {
        HeadInsertEventTap = 0,
        TailAppendEventTap = 1,
    }

    public enum CGScrollEventUnit : uint
    {
        Pixel = 0,
        Line = 1,
    }

    public enum CGEventTapOptions : uint
    {
        Default = 0x00000000,
        ListenOnly = 0x00000001,
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
        TapDisabledByUserInput = 0xFFFFFFFF,
    }

    public enum CGMouseButton : uint
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    [Flags]
    public enum CGEventModifiers : ulong
    {
        NonCoalesced = 0x0000000000000100,
        AlphaShift = 0x0000000000010000, // Caps Lock
        Shift = 0x0000000000020000,
        Control = 0x0000000000040000,
        Alternate = 0x0000000000080000, // Option
        Command = 0x0000000000100000,
        NumericPad = 0x0000000000200000,
        Help = 0x0000000000400000,
        SecondaryFn = 0x0000000000800000,
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
        ScrollWheelEventIsContinuous = 88,
        ScrollWheelEventInstantMouser = 14,
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

    [Flags]
    public enum CGWindowListOption : uint
    {
        OnScreenOnly = 1,
        IncludingWindow = 8,
        ExcludeDesktopElements = 16,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint origin;
        public CGSize size;
    }

    [LibraryImport(CoreGraphicsLib)]
    public static partial uint CGMainDisplayID();

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGRect CGDisplayBounds(uint display);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGError CGGetOnlineDisplayList(uint maxDisplays, [Out] uint[]? onlineDisplays, out uint displayCount);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGError CGGetActiveDisplayList(uint maxDisplays, [Out] uint[]? activeDisplays, out uint displayCount);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGError CGGetDisplaysWithRect(CGRect rect, uint maxDisplays, [Out] uint[]? displays, out uint displayCount);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGDisplayCreateImageForRect(uint display, CGRect rect);

    [LibraryImport(CoreGraphicsLib)]
    public static partial void CGImageRelease(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGImageGetDataProvider(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial IntPtr CGDataProviderCopyData(IntPtr provider);

    [LibraryImport(CoreGraphicsLib)]
    public static partial nuint CGImageGetWidth(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial nuint CGImageGetHeight(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial nuint CGImageGetBitsPerComponent(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial nuint CGImageGetBitsPerPixel(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial nuint CGImageGetBytesPerRow(IntPtr image);

    [LibraryImport(CoreGraphicsLib)]
    public static partial CGBitmapInfo CGImageGetBitmapInfo(IntPtr image);

    public const uint kCGBitmapAlphaInfoMask = 0x1F;
    public const uint kCGBitmapByteOrderMask = 0x7000;
    public const uint kCGBitmapByteOrder32Little = 0x2000;
    public const uint kCGBitmapByteOrder32Big = 0x4000;

    public enum CGError
    {
        Success = 0,
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
        ByteOrder32Big = kCGBitmapByteOrder32Big,
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
            return function is not null && function() is not 0;
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
