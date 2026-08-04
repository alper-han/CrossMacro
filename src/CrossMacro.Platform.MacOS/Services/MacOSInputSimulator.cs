
namespace CrossMacro.Platform.MacOS.Services;

public sealed class MacOSInputSimulator :
    IInputSimulator,
    IInputSimulatorCapabilities,
    ITaggedKeyboardInputSimulator,
    ITaggedUnicodeTextInputSimulator,
    IPlatformPasteShortcutProvider
{
    private readonly Lock _keyboardLock = new();
    private readonly Lock _mouseLock = new();
    private readonly Func<bool> _requestPostEventAccess;
    private readonly Func<bool> _isMacOS;
    private readonly Func<CoreGraphics.CGPoint?> _getCursorPosition;
    private readonly Func<int, int, IReadOnlySet<int>, bool> _postMouseMovement;
    private readonly Func<int, bool, int, int, bool> _postMouseButton;
    private readonly HashSet<int> _pressedModifierKeys = [];
    private readonly HashSet<int> _pressedMouseButtons = [];
    private CoreGraphics.CGEventModifiers _keyboardFlags;
    private bool _postEventPermissionGranted;
    private bool _hasPostedMousePosition;
    private bool _hasPendingPostedMousePosition;
    private int _lastPostedMouseX;
    private int _lastPostedMouseY;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool SupportsUnicodeTextInput => IsSupported;
    public bool SupportsTaggedKeyboardInput => IsSupported;
    public bool SupportsAbsoluteCoordinates => true;
    public bool UsesMetaKeyForStandardPaste => true;

    public MacOSInputSimulator()
        : this(MacOSPermissionChecker.RequestPostEventAccess) { /* Empty */ }

    internal MacOSInputSimulator(
        Func<bool> requestPostEventAccess,
        Func<bool>? isMacOS = null,
        Func<CoreGraphics.CGPoint?>? getCursorPosition = null,
        Func<int, int, IReadOnlySet<int>, bool>? postMouseMovement = null,
        Func<int, bool, int, int, bool>? postMouseButton = null)
    {
        _requestPostEventAccess = requestPostEventAccess ?? throw new ArgumentNullException(nameof(requestPostEventAccess));
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
        _getCursorPosition = getCursorPosition ?? GetCursorPosition;
        _postMouseMovement = postMouseMovement ?? PostMouseMovement;
        _postMouseButton = postMouseButton ?? PostMouseButton;
    }

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        if (!_isMacOS())
        {
            return;
        }

        lock (_mouseLock)
        {
            if (_getCursorPosition() is { } position)
            {
                TrackMousePosition((int)position.X, (int)position.Y, pending: false);
            }
        }
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        lock (_mouseLock)
        {
            RequestPostEventAccessOnce();
            if (_postMouseMovement(x, y, _pressedMouseButtons))
            {
                TrackMousePosition(x, y, pending: true);
            }
        }
    }

    public void MoveRelative(int dx, int dy)
    {
        lock (_mouseLock)
        {
            RequestPostEventAccessOnce();
            if (!_hasPostedMousePosition)
            {
                if (_getCursorPosition() is not { } current)
                {
                    return;
                }

                TrackMousePosition((int)current.X, (int)current.Y, pending: false);
            }

            int targetX = (int)Math.Clamp((long)_lastPostedMouseX + dx, int.MinValue, int.MaxValue);
            int targetY = (int)Math.Clamp((long)_lastPostedMouseY + dy, int.MinValue, int.MaxValue);
            if (_postMouseMovement(targetX, targetY, _pressedMouseButtons))
            {
                TrackMousePosition(targetX, targetY, pending: true);
            }
        }
    }

    public void MouseButton(int button, bool pressed)
    {
        lock (_mouseLock)
        {
            RequestPostEventAccessOnce();
            if (!TryResolveMouseButton(button, pressed, out _, out _, out _))
            {
                return;
            }

            var target = ResolveMouseButtonPosition();
            if (!_postMouseButton(button, pressed, target.X, target.Y))
            {
                return;
            }

            TrackMousePosition(target.X, target.Y, pending: false);
            if (pressed)
            {
                _ = _pressedMouseButtons.Add(button);
            }
            else
            {
                _ = _pressedMouseButtons.Remove(button);
            }
        }
    }

    internal static bool TryResolveMouseButton(
        int button,
        bool pressed,
        out CoreGraphics.CGMouseButton macButton,
        out CoreGraphics.CGEventType eventType,
        out long buttonNumber)
    {
        buttonNumber = button switch
        {
            MouseButtonCode.Left => 0,
            MouseButtonCode.Right => 1,
            MouseButtonCode.Middle => 2,
            MouseButtonCode.Side1 => 3,
            MouseButtonCode.Side2 => 4,
            _ => -1,
        };

        if (buttonNumber < 0)
        {
            macButton = default;
            eventType = default;
            return false;
        }

        macButton = (CoreGraphics.CGMouseButton)buttonNumber;
        eventType = button switch
        {
            MouseButtonCode.Left => pressed ? CoreGraphics.CGEventType.LeftMouseDown : CoreGraphics.CGEventType.LeftMouseUp,
            MouseButtonCode.Right => pressed ? CoreGraphics.CGEventType.RightMouseDown : CoreGraphics.CGEventType.RightMouseUp,
            MouseButtonCode.Middle or MouseButtonCode.Side1 or MouseButtonCode.Side2 =>
                pressed ? CoreGraphics.CGEventType.OtherMouseDown : CoreGraphics.CGEventType.OtherMouseUp,
            _ => throw new UnreachableException(),
        };
        return true;
    }

    internal static (CoreGraphics.CGEventType EventType, CoreGraphics.CGMouseButton Button, long ButtonNumber)
        ResolveMouseMovement(IReadOnlySet<int> pressedButtons)
    {
        ArgumentNullException.ThrowIfNull(pressedButtons);

        int button = -1;
        if (pressedButtons.Contains(MouseButtonCode.Left))
        {
            button = MouseButtonCode.Left;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Right))
        {
            button = MouseButtonCode.Right;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Middle))
        {
            button = MouseButtonCode.Middle;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Side1))
        {
            button = MouseButtonCode.Side1;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Side2))
        {
            button = MouseButtonCode.Side2;
        }

        return button switch
        {
            MouseButtonCode.Left => (CoreGraphics.CGEventType.LeftMouseDragged, CoreGraphics.CGMouseButton.Left, 0),
            MouseButtonCode.Right => (CoreGraphics.CGEventType.RightMouseDragged, CoreGraphics.CGMouseButton.Right, 1),
            MouseButtonCode.Middle => (CoreGraphics.CGEventType.OtherMouseDragged, CoreGraphics.CGMouseButton.Center, 2),
            MouseButtonCode.Side1 => (CoreGraphics.CGEventType.OtherMouseDragged, (CoreGraphics.CGMouseButton)3, 3),
            MouseButtonCode.Side2 => (CoreGraphics.CGEventType.OtherMouseDragged, (CoreGraphics.CGMouseButton)4, 4),
            _ => (CoreGraphics.CGEventType.MouseMoved, CoreGraphics.CGMouseButton.Left, 0),
        };
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

    public void Sync() { /* Empty */ }

    public void Dispose() { /* Empty */ }

    private static void TypeTextCore(string text, long? marker)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length is 0)
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

    private static CoreGraphics.CGPoint? GetCursorPosition()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CoreGraphics.CGEventGetLocation(eventRef);
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    private static bool PostMouseMovement(
        int x,
        int y,
        IReadOnlySet<int> pressedMouseButtons)
    {
        var (eventType, mouseButton, buttonNumber) = ResolveMouseMovement(pressedMouseButtons);
        var point = new CoreGraphics.CGPoint { X = x, Y = y };
        var eventRef = CoreGraphics.CGEventCreateMouseEvent(IntPtr.Zero, eventType, point, mouseButton);
        if (eventRef == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (eventType is CoreGraphics.CGEventType.OtherMouseDragged)
            {
                CoreGraphics.CGEventSetIntegerValueField(
                    eventRef,
                    CoreGraphics.CGEventField.MouseEventButtonNumber,
                    buttonNumber);
            }

            CoreGraphics.CGEventSetFlags(eventRef, CoreGraphics.CGEventModifiers.NonCoalesced);
            CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
            return true;
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    private static bool PostMouseButton(int button, bool pressed, int x, int y)
    {
        if (!TryResolveMouseButton(button, pressed, out var macButton, out var eventType, out var buttonNumber))
        {
            return false;
        }

        var point = new CoreGraphics.CGPoint { X = x, Y = y };
        var eventRef = CoreGraphics.CGEventCreateMouseEvent(IntPtr.Zero, eventType, point, macButton);
        if (eventRef == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (button is not MouseButtonCode.Left and not MouseButtonCode.Right)
            {
                CoreGraphics.CGEventSetIntegerValueField(
                    eventRef,
                    CoreGraphics.CGEventField.MouseEventButtonNumber,
                    buttonNumber);
            }

            CoreGraphics.CGEventPost(CoreGraphics.CGEventTapLocation.HIDEventTap, eventRef);
            return true;
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    private (int X, int Y) ResolveMouseButtonPosition()
    {
        if ((_hasPendingPostedMousePosition || _pressedMouseButtons.Count > 0) && _hasPostedMousePosition)
        {
            return (_lastPostedMouseX, _lastPostedMouseY);
        }

        if (_getCursorPosition() is { } currentPosition)
        {
            return ((int)currentPosition.X, (int)currentPosition.Y);
        }

        return (_lastPostedMouseX, _lastPostedMouseY);
    }

    private void TrackMousePosition(int x, int y, bool pending)
    {
        _lastPostedMouseX = x;
        _lastPostedMouseY = y;
        _hasPostedMousePosition = true;
        _hasPendingPostedMousePosition = pending;
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
            if (route is MacOSKeyboardEventRoute.SystemDefined)
            {
                PostSystemDefinedKeyEvent(nxKeyType, pressed, marker, flags);
                return;
            }

            if (route is MacOSKeyboardEventRoute.Unsupported)
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
        CoreGraphics.CGEventModifiers activeModifierFlags)
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
        if (virtualKeyCode is 0xFFFF)
        {
            return MacOSKeyboardEventRoute.Unsupported;
        }

        return MacOSKeyboardEventRoute.Keyboard;
    }

    internal CoreGraphics.CGEventModifiers UpdateKeyboardFlags(int keyCode, bool pressed)
    {
        lock (_keyboardLock)
        {
            return UpdateKeyboardFlagsCore(keyCode, pressed);
        }
    }

    private CoreGraphics.CGEventModifiers UpdateKeyboardFlagsCore(int keyCode, bool pressed)
    {
        if (GetModifierFlag(keyCode) == default)
        {
            return _keyboardFlags;
        }

        if (pressed)
        {
            _ = _pressedModifierKeys.Add(keyCode);
        }
        else
        {
            _ = _pressedModifierKeys.Remove(keyCode);
        }

        _keyboardFlags = CreateKeyboardFlags(_pressedModifierKeys);
        return _keyboardFlags;
    }

    internal static CoreGraphics.CGEventModifiers CreateKeyboardFlags(IEnumerable<int> pressedModifierKeys)
    {
        var flags = default(CoreGraphics.CGEventModifiers);
        foreach (var keyCode in pressedModifierKeys)
        {
            flags |= GetModifierFlag(keyCode);
        }

        return flags;
    }

    private static CoreGraphics.CGEventModifiers GetModifierFlag(int keyCode)
    {
        return keyCode switch
        {
            InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT => CoreGraphics.CGEventModifiers.Shift,
            InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL => CoreGraphics.CGEventModifiers.Control,
            InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT => CoreGraphics.CGEventModifiers.Alternate,
            InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA => CoreGraphics.CGEventModifiers.Command,
            InputEventCode.KEY_CAPSLOCK => CoreGraphics.CGEventModifiers.AlphaShift,
            _ => default,
        };
    }

    private static void ApplyKeyboardMarker(IntPtr eventRef, long? marker)
    {
        if (marker is not null)
        {
            CoreGraphics.CGEventSetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSourceUserData, marker.Value);
        }
    }
}
