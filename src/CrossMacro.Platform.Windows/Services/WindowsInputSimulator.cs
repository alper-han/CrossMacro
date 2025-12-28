using System.Runtime.InteropServices;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Helpers;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

public class WindowsInputSimulator : IInputSimulator
{
    private int _screenWidth;
    private int _screenHeight;

    public string ProviderName => "Windows SendInput";
    public bool IsSupported => OperatingSystem.IsWindows();

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        
        if (_screenWidth <= 0 || _screenHeight <= 0)
        {
            _screenWidth = User32.GetSystemMetrics(User32.SM_CXSCREEN);
            _screenHeight = User32.GetSystemMetrics(User32.SM_CYSCREEN);
        }
    }

    public void MoveAbsolute(int x, int y)
    {
        var input = new INPUT
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = CalculateAbsoluteCoordinate(x, _screenWidth),
                    dy = CalculateAbsoluteCoordinate(y, _screenHeight),
                    dwFlags = MouseEventFlags.MOUSEEVENTF_ABSOLUTE | MouseEventFlags.MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(input);
    }

    public void MoveRelative(int dx, int dy)
    {
        var input = new INPUT
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(input);
    }

    public void MouseButton(int button, bool pressed)
    {
        uint flags = 0;
        
        switch (button)
        {
            case InputEventCode.BTN_LEFT:
                flags = pressed ? MouseEventFlags.MOUSEEVENTF_LEFTDOWN : MouseEventFlags.MOUSEEVENTF_LEFTUP;
                break;
            case InputEventCode.BTN_RIGHT:
                flags = pressed ? MouseEventFlags.MOUSEEVENTF_RIGHTDOWN : MouseEventFlags.MOUSEEVENTF_RIGHTUP;
                break;
            case InputEventCode.BTN_MIDDLE:
                flags = pressed ? MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN : MouseEventFlags.MOUSEEVENTF_MIDDLEUP;
                break;
            default:
                return;
        }

        var input = new INPUT
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        
        SendInput(input);
    }

    private const int WHEEL_DELTA = 120;

    public void Scroll(int delta)
    {
        int normalizedDelta = Math.Abs(delta) <= 10 ? delta * WHEEL_DELTA : delta;
        
        var input = new INPUT
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    mouseData = (uint)normalizedDelta,
                    dwFlags = MouseEventFlags.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        
        SendInput(input);
    }

    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 
        0x2D, 0x2E, 
        0x5B, 0x5C, 
        0xA3, 0xA5, 
    };

    public void KeyPress(int keyCode, bool pressed)
    {
        ushort vk = WindowsKeyMap.GetVirtualKey(keyCode);
        if (vk == 0) return; 

        uint flags = pressed ? 0u : KeyEventFlags.KEYEVENTF_KEYUP;
        
        if (ExtendedKeys.Contains(vk))
        {
            flags |= KeyEventFlags.KEYEVENTF_EXTENDEDKEY;
        }
        
        var input = new INPUT
        {
            type = InputType.INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        
        SendInput(input);
    }

    public void Sync()
    {
    }

    public void Dispose()
    {
    }

    private static int CalculateAbsoluteCoordinate(int val, int max)
    {
        if (max <= 0) return 0; 
        return (val * 65535) / max;
    }

    private void SendInput(INPUT input)
    {
        User32.SendInput(1, new[] { input }, INPUT.Size);
    }
}
