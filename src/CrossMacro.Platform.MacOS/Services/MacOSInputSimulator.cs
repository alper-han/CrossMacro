using System;
using System.Runtime.InteropServices;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSInputSimulator :
    IInputSimulator,
    IInputSimulatorCapabilities,
    IUnicodeTextInputSimulator,
    ITaggedKeyboardInputSimulator,
    ITaggedUnicodeTextInputSimulator
{
    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool SupportsUnicodeTextInput => IsSupported;
    public bool SupportsTaggedKeyboardInput => IsSupported;
    public bool SupportsAbsoluteCoordinates => true;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
    }

    public void MoveAbsolute(int x, int y)
    {
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
        TypeTextCore(text, marker: null);
    }

    public void TypeTextTagged(string text, long tag)
    {
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

        var codeUnits = text.ToCharArray();
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

    private static void PostUnicodeKeyboardEvent(char[] codeUnits, bool keyDown, long? marker)
    {
        var eventRef = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, 0, keyDown);
        ApplyKeyboardMarker(eventRef, marker);
        CoreGraphics.CGEventKeyboardSetUnicodeString(eventRef, (nuint)codeUnits.Length, codeUnits);
        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    private static void PostKeyboardEvent(int keyCode, bool pressed, long? marker)
    {
        var ushortCode = KeyMap.ToMacKey(keyCode);
        if (ushortCode == 0xFFFF) return;

        var eventRef = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, ushortCode, pressed);
        ApplyKeyboardMarker(eventRef, marker);
        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    private static void ApplyKeyboardMarker(IntPtr eventRef, long? marker)
    {
        if (marker.HasValue)
        {
            CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSourceUserData, marker.Value);
        }
    }
}
