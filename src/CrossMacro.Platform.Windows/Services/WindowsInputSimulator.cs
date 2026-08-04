
namespace CrossMacro.Platform.Windows.Services;

public sealed class WindowsInputSimulator :
    IInputSimulator,
    IInputSimulatorCapabilities,
    ITaggedKeyboardInputSimulator,
    ITaggedUnicodeTextInputSimulator
{
    private ScreenRect? _desktopBounds;

    // ThreadStatic ensures each thread has its own buffer - thread-safe without locking
    [field: ThreadStatic]
    private static InputStruct[] InputBuffer { get => field ??= new InputStruct[1]; }

    public string ProviderName => "Windows SendInput";
    public bool IsSupported => OperatingSystem.IsWindows();
    public bool SupportsUnicodeTextInput => IsSupported;
    public bool SupportsTaggedKeyboardInput => IsSupported;
    public bool SupportsAbsoluteCoordinates => true;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        _desktopBounds = WindowsMousePositionProvider.ReadDesktopBounds(User32.GetSystemMetrics)
            ?? (screenWidth > 0 && screenHeight > 0 ? new ScreenRect(0, 0, screenWidth, screenHeight) : null);
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        SendInput(CreateAbsoluteMouseInput(x, y, _desktopBounds));
    }

    public void MoveRelative(int dx, int dy)
    {
        var input = new InputStruct
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE | MouseEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };

        SendInput(input);
    }

    public void MouseButton(int button, bool pressed)
    {
        if (TryCreateMouseButtonInput(button, pressed, out var input))
        {
            SendInput(input);
        }
    }

    internal static bool TryCreateMouseButtonInput(int button, bool pressed, out InputStruct input)
    {
        uint flags;
        uint mouseData = 0;

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
            case InputEventCode.BTN_SIDE:
                flags = pressed ? MouseEventFlags.MOUSEEVENTF_XDOWN : MouseEventFlags.MOUSEEVENTF_XUP;
                mouseData = User32.XBUTTON1;
                break;
            case InputEventCode.BTN_EXTRA:
                flags = pressed ? MouseEventFlags.MOUSEEVENTF_XDOWN : MouseEventFlags.MOUSEEVENTF_XUP;
                mouseData = User32.XBUTTON2;
                break;
            default:
                input = default;
                return false;
        }

        input = new InputStruct
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        return true;
    }

    private const int WHEEL_DELTA = 120;

    public void Scroll(int delta, bool isHorizontal = false)
    {
        SendInput(CreateScrollInput(delta, isHorizontal));
    }

    internal static InputStruct CreateScrollInput(int delta, bool isHorizontal)
    {
        int normalizedDelta = Math.Abs(delta) <= 10 ? delta * WHEEL_DELTA : delta;

        return new InputStruct
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    mouseData = (uint)normalizedDelta,
                    dwFlags = isHorizontal ? MouseEventFlags.MOUSEEVENTF_HWHEEL : MouseEventFlags.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
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
        SendKeyPress(keyCode, pressed, marker: null);
    }

    public void KeyPressTagged(int keyCode, bool pressed, long tag)
    {
        SendKeyPress(keyCode, pressed, tag);
    }

    public void TypeText(string text)
    {
        TypeTextCore(text, marker: null);
    }

    public void TypeTextTagged(string text, long tag)
    {
        TypeTextCore(text, tag);
    }

    public void Sync() { /* Empty */ }

    public void Dispose() { /* Empty */ }

    private static void SendKeyPress(int keyCode, bool pressed, long? marker)
    {
        if (TryCreateKeyboardInput(keyCode, pressed, marker, out var input))
        {
            SendInput(input);
        }
    }

    internal static bool TryCreateKeyboardInput(int keyCode, bool pressed, long? marker, out InputStruct input)
    {
        ushort virtualKey = WindowsKeyMap.GetVirtualKey(keyCode);
        if (virtualKey is 0)
        {
            input = default;
            return false;
        }

        uint flags = pressed ? 0u : KeyEventFlags.KEYEVENTF_KEYUP;

        if (ExtendedKeys.Contains(virtualKey) || keyCode == InputEventCode.KEY_KPENTER)
        {
            flags |= KeyEventFlags.KEYEVENTF_EXTENDEDKEY;
        }

        input = new InputStruct
        {
            type = InputType.INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KeybdInput
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = marker is not null ? InputEventMarkers.ToIntPtr(marker.Value) : IntPtr.Zero,
                },
            },
        };
        return true;
    }

    internal static bool TryCreateKeyboardInput(int keyCode, bool pressed, out InputStruct input)
    {
        return TryCreateKeyboardInput(keyCode, pressed, marker: null, out input);
    }

    private static void TypeTextCore(string text, long? marker)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (char codeUnit in text)
        {
            SendUnicodeInput(codeUnit, keyUp: false, marker);
            SendUnicodeInput(codeUnit, keyUp: true, marker);
        }
    }

    internal static InputStruct CreateAbsoluteMouseInput(int x, int y, ScreenRect? desktopBounds)
    {
        var bounds = desktopBounds ?? new ScreenRect(0, 0, 1, 1);
        return new InputStruct
        {
            type = InputType.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = CalculateAbsoluteCoordinate(x, bounds.X, bounds.Width),
                    dy = CalculateAbsoluteCoordinate(y, bounds.Y, bounds.Height),
                    dwFlags = MouseEventFlags.MOUSEEVENTF_ABSOLUTE
                        | MouseEventFlags.MOUSEEVENTF_VIRTUALDESK
                        | MouseEventFlags.MOUSEEVENTF_MOVE
                        | MouseEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
    }

    private static int CalculateAbsoluteCoordinate(int value, int origin, int extent)
    {
        if (extent <= 1)
        {
            return 0;
        }

        long offset = Math.Clamp((long)value - origin, 0, extent - 1L);
        return (int)Math.Round(offset * 65535d / (extent - 1L), MidpointRounding.AwayFromZero);
    }

    private static void SendInput(InputStruct input)
    {
        var buffer = InputBuffer;
        buffer[0] = input;
        _ = User32.SendInput(1, buffer, InputStruct.Size);
    }

    private static void SendUnicodeInput(char codeUnit, bool keyUp, long? marker)
    {
        SendKeyboardInput(
            virtualKey: 0,
            scanCode: codeUnit,
            flags: KeyEventFlags.KEYEVENTF_UNICODE | (keyUp ? KeyEventFlags.KEYEVENTF_KEYUP : 0),
            marker);
    }

    private static void SendKeyboardInput(ushort virtualKey, ushort scanCode, uint flags, long? marker)
    {
        var input = new InputStruct
        {
            type = InputType.INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KeybdInput
                {
                    wVk = virtualKey,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = marker is not null ? InputEventMarkers.ToIntPtr(marker.Value) : IntPtr.Zero,
                },
            },
        };

        SendInput(input);
    }
}
