using System;
using System.Runtime.InteropServices;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSInputSimulator : IInputSimulator
{
    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

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

    public void Scroll(int delta)
    {
        var eventRef = CoreGraphics.CGEventCreateScrollWheelEvent(
            IntPtr.Zero,
            CoreGraphics.CGScrollEventUnit.Line,
            1,
            delta
        );
        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        var ushortCode = KeyMap.ToMacKey(keyCode);
        if (ushortCode == 0xFFFF) return;

        var eventRef = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, ushortCode, pressed);
        CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
        CoreFoundation.CFRelease(eventRef);
    }

    public void Sync()
    {
    }

    public void Dispose()
    {
    }
    
    private CoreGraphics.CGPoint GetCursorPos()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        CoreFoundation.CFRelease(eventRef);
        return loc;
    }
}
