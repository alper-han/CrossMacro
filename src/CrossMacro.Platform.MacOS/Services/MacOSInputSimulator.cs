using System;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Helpers;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSInputSimulator :
    IInputSimulator,
    IInputSimulatorCapabilities,
    IUnicodeTextInputSimulator,
    ITaggedKeyboardInputSimulator,
    ITaggedUnicodeTextInputSimulator,
    IPlatformPasteShortcutProvider
{
    private readonly object _keyboardLock = new();
    private readonly Func<bool> _requestPostEventAccess;
    private readonly Func<bool> _isMacOS;
    private readonly HashSet<int> _pressedModifierKeys = [];
    private CoreGraphics.CGEventFlags _keyboardFlags;
    private bool _postEventPermissionGranted;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool SupportsUnicodeTextInput => IsSupported;
    public bool SupportsTaggedKeyboardInput => IsSupported;
    public bool SupportsAbsoluteCoordinates => true;
    public bool UsesMetaKeyForStandardPaste => true;

    public MacOSInputSimulator()
        : this(MacOSPermissionChecker.RequestPostEventAccess)
    {
    }

    internal MacOSInputSimulator(Func<bool> requestPostEventAccess, Func<bool>? isMacOS = null)
    {
        _requestPostEventAccess = requestPostEventAccess ?? throw new ArgumentNullException(nameof(requestPostEventAccess));
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
    }

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
    }

    public void MoveAbsolute(int x, int y)
    {
        RequestPostEventAccessOnce();
        var point = new CoreGraphics.CGPoint { X = x, Y = y };
         var eventRef = CoreGraphics.CGEventCreateMouseEvent(
             IntPtr.Zero, 
             CoreGraphics.CGEventType.MouseMoved, 
             point, 
             CoreGraphics.CGMouseButton.Left 
         );
         CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
         CoreFoundation.CFRelease(eventRef);
    }

    public void MoveRelative(int dx, int dy)
    {
        var current = GetCursorPos();
        MoveAbsolute((int)current.X + dx, (int)current.Y + dy);
    }

    public void MouseButton(int button, bool pressed)
    {
        RequestPostEventAccessOnce();
        var current = GetCursorPos();
        
        CoreGraphics.CGMouseButton macBtn = CoreGraphics.CGMouseButton.Left;
        CoreGraphics.CGEventType type = CoreGraphics.CGEventType.Null;

        switch (button)
        {
            case MouseButtonCode.Left:
                macBtn = CoreGraphics.CGMouseButton.Left;
                type = pressed ? CoreGraphics.CGEventType.LeftMouseDown : CoreGraphics.CGEventType.LeftMouseUp;
                break;
            case MouseButtonCode.Right:
                macBtn = CoreGraphics.CGMouseButton.Right;
                type = pressed ? CoreGraphics.CGEventType.RightMouseDown : CoreGraphics.CGEventType.RightMouseUp;
                break;
            case MouseButtonCode.Middle:
                macBtn = CoreGraphics.CGMouseButton.Center;
                type = pressed ? CoreGraphics.CGEventType.OtherMouseDown : CoreGraphics.CGEventType.OtherMouseUp;
                break;
            default:
                macBtn = CoreGraphics.CGMouseButton.Center; 
                type = pressed ? CoreGraphics.CGEventType.OtherMouseDown : CoreGraphics.CGEventType.OtherMouseUp;
                break;
        }

        var eventRef = CoreGraphics.CGEventCreateMouseEvent(IntPtr.Zero, type, current, macBtn);
        
        if (button != MouseButtonCode.Left && button != MouseButtonCode.Right && button != MouseButtonCode.Middle)
        {
             long btnNum = button; 
             CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventButtonNumber, btnNum);
        }

        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        RequestPostEventAccessOnce();
        var eventRef = isHorizontal
            ? CoreGraphics.CGEventCreateScrollWheelEvent2(
                IntPtr.Zero,
                CoreGraphics.CGScrollEventUnit.Line,
                2,
                0,
                delta)
            : CoreGraphics.CGEventCreateScrollWheelEvent(
                IntPtr.Zero,
                CoreGraphics.CGScrollEventUnit.Line,
                1,
                delta);

        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        PostKeyboardEvent(keyCode, pressed, marker: null);
    }

    public void KeyPressTagged(int keyCode, bool pressed, long tag)
    {
        PostKeyboardEvent(keyCode, pressed, tag);
    }

    public void TypeText(string text)
    {
        RequestPostEventAccessOnce();
        TypeTextCore(text, marker: null);
    }

    public void TypeTextTagged(string text, long tag)
    {
        RequestPostEventAccessOnce();
        TypeTextCore(text, tag);
    }

    public void Sync()
    {
    }

    public void Dispose()
    {
    }

    private static void TypeTextCore(string text, long? marker)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return;
        }

        var codeUnits = new ushort[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            codeUnits[i] = text[i];
        }
        PostUnicodeKeyboardEvent(codeUnits, keyDown: true, marker);
        PostUnicodeKeyboardEvent(codeUnits, keyDown: false, marker);
    }

    private CoreGraphics.CGPoint GetCursorPos()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        CoreFoundation.CFRelease(eventRef);
        return loc;
    }

    private static void PostUnicodeKeyboardEvent(ushort[] codeUnits, bool keyDown, long? marker)
    {
        var eventRef = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, 0, keyDown);
        ApplyKeyboardMarker(eventRef, marker);
        CoreGraphics.CGEventKeyboardSetUnicodeString(eventRef, (nuint)codeUnits.Length, codeUnits);
        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    private void PostKeyboardEvent(int keyCode, bool pressed, long? marker)
    {
        lock (_keyboardLock)
        {
            RequestPostEventAccessOnce();
            var flags = UpdateKeyboardFlagsCore(keyCode, pressed);

            var route = ResolveKeyboardEventRoute(keyCode, out var nxKeyType, out var ushortCode);
            if (route == MacOSKeyboardEventRoute.SystemDefined)
            {
                PostSystemDefinedKeyEvent(nxKeyType, pressed, marker, flags);
                return;
            }

            if (route == MacOSKeyboardEventRoute.Unsupported)
            {
                return;
            }

            var eventRef = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, ushortCode, pressed);
            if (eventRef == IntPtr.Zero)
            {
                return;
            }

            try
            {
                ApplyKeyboardMarker(eventRef, marker);
                CoreGraphics.CGEventSetFlags(eventRef, flags);
                CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
            }
            finally
            {
                CoreFoundation.CFRelease(eventRef);
            }
        }
    }

    private void RequestPostEventAccessOnce()
    {
        if (_postEventPermissionGranted || !_isMacOS())
        {
            return;
        }

        if (!_requestPostEventAccess())
        {
            ThrowPlaybackPermissionRequired();
        }

        _postEventPermissionGranted = true;
    }

    private static void ThrowPlaybackPermissionRequired()
    {
        throw new InputInjectionPermissionRequiredException(
            "macOS Accessibility permission is required for playback and input injection. Input Monitoring only allows CrossMacro to capture and record input. Open System Settings > Privacy & Security > Accessibility, approve CrossMacro, then try playback again.");
    }

    private static void PostSystemDefinedKeyEvent(
        int nxKeyType,
        bool pressed,
        long? marker,
        CoreGraphics.CGEventFlags activeModifierFlags)
    {
        if (!MacOSSystemKeyEventFactory.TryCreateEvent(
                nxKeyType,
                pressed,
                marker,
                activeModifierFlags,
                out var eventRef))
        {
            return;
        }

        try
        {
            CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    internal static bool TryGetSystemDefinedKeyType(int keyCode, out int nxKeyType)
    {
        return MacOSSystemKeyEventFactory.TryGetNxKeyType(keyCode, out nxKeyType);
    }

    internal static MacOSKeyboardEventRoute ResolveKeyboardEventRoute(
        int keyCode,
        out int nxKeyType,
        out ushort virtualKeyCode)
    {
        if (MacOSSystemKeyEventFactory.TryGetNxKeyType(keyCode, out nxKeyType))
        {
            virtualKeyCode = 0xFFFF;
            return MacOSKeyboardEventRoute.SystemDefined;
        }

        virtualKeyCode = KeyMap.ToMacKey(keyCode);
        if (virtualKeyCode == 0xFFFF)
        {
            return MacOSKeyboardEventRoute.Unsupported;
        }

        return MacOSKeyboardEventRoute.Keyboard;
    }

    internal CoreGraphics.CGEventFlags UpdateKeyboardFlags(int keyCode, bool pressed)
    {
        lock (_keyboardLock)
        {
            return UpdateKeyboardFlagsCore(keyCode, pressed);
        }
    }

    private CoreGraphics.CGEventFlags UpdateKeyboardFlagsCore(int keyCode, bool pressed)
    {
        if (GetModifierFlag(keyCode) == 0)
        {
            return _keyboardFlags;
        }

        if (pressed)
        {
            _pressedModifierKeys.Add(keyCode);
        }
        else
        {
            _pressedModifierKeys.Remove(keyCode);
        }

        _keyboardFlags = CreateKeyboardFlags(_pressedModifierKeys);
        return _keyboardFlags;
    }

    internal static CoreGraphics.CGEventFlags CreateKeyboardFlags(IEnumerable<int> pressedModifierKeys)
    {
        var flags = default(CoreGraphics.CGEventFlags);
        foreach (var keyCode in pressedModifierKeys)
        {
            flags |= GetModifierFlag(keyCode);
        }

        return flags;
    }

    private static CoreGraphics.CGEventFlags GetModifierFlag(int keyCode)
    {
        return keyCode switch
        {
            InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT => CoreGraphics.CGEventFlags.Shift,
            InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL => CoreGraphics.CGEventFlags.Control,
            InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT => CoreGraphics.CGEventFlags.Alternate,
            InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA => CoreGraphics.CGEventFlags.Command,
            InputEventCode.KEY_CAPSLOCK => CoreGraphics.CGEventFlags.AlphaShift,
            _ => 0
        };
    }

    private static void ApplyKeyboardMarker(IntPtr eventRef, long? marker)
    {
        if (marker.HasValue)
        {
            CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSourceUserData, marker.Value);
        }
    }
}
