
namespace CrossMacro.Platform.Windows.Services.Input;

/// <summary>
/// Turns low-level hook and raw-input data into <see cref="CapturedInputEvent"/>s.
/// Runs on the message-pump thread, so it only translates and hands each event to
/// <paramref name="emit"/>; it never invokes subscribers.
/// </summary>
internal sealed class WindowsHookEventTranslator(
    Action<CapturedInputEvent> emit,
    bool useAbsoluteCoordinates,
    bool useRawRelativeCoordinates)
{
    private const string MouseDeviceName = "VirtualMouse";
    private const string RawMouseDeviceName = "RawMouse";
    private const string KeyboardDeviceName = "VirtualKeyboard";

    private readonly HashSet<ushort> _reportedUnmappedKeys = [];
    private readonly ScrollNotchAccumulator _verticalScroll = new();
    private readonly ScrollNotchAccumulator _horizontalScroll = new();
    private int _lastX;
    private int _lastY;
    private bool _hasLastPosition;

    public void SetCursorPosition(int x, int y)
    {
        _lastX = x;
        _lastY = y;
        _hasLastPosition = true;
    }

    public void OnMouseHook(uint message, MsllHookStruct hook)
    {
        if (message == User32.WM_MOUSEMOVE && !useRawRelativeCoordinates)
        {
            HandleMouseMove(hook.pt.x, hook.pt.y);
        }
        else if (WindowsInputEventPolicy.TryMapMouseButtonOrScroll(message, hook.mouseData, out ushort code, out int value, out ushort type))
        {
            HandleMouseButtonOrScroll(code, value, type);
        }
    }

    public void OnKeyboardHook(uint message, KbdllHookStruct hook)
    {
        if (WindowsInputEventPolicy.ShouldIgnoreKeyboardHookEvent(hook.flags, hook.dwExtraInfo))
        {
            return;
        }

        bool isDown = message is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN;
        if (!isDown && message is not (User32.WM_KEYUP or User32.WM_SYSKEYUP))
        {
            return;
        }

        int evdevCode = WindowsInputEventPolicy.MapKeyboardEvent((ushort)hook.vkCode, hook.flags);
        if (evdevCode is not 0)
        {
            emit(new CapturedInputEvent
            {
                Type = InputEventType.Key,
                Code = evdevCode,
                Value = isDown ? 1 : 0,
                Timestamp = CaptureTimestamps.UnixMilliseconds(),
                TimestampMicroseconds = CaptureTimestamps.MonotonicMicroseconds(),
                DeviceName = KeyboardDeviceName,
            });
        }
        else if (isDown && _reportedUnmappedKeys.Add((ushort)hook.vkCode))
        {
            Log.Warning("[WindowsInputCapture] Unmapped virtual key 0x{VKHex} is not recorded", hook.vkCode.ToString("X2", CultureInfo.InvariantCulture));
        }
    }

    public void OnRawMouseMovement(int deltaX, int deltaY)
    {
        long timestamp = CaptureTimestamps.UnixMilliseconds();
        long timestampMicroseconds = CaptureTimestamps.MonotonicMicroseconds();
        if (deltaX is not 0)
        {
            EmitMove(InputEventCode.REL_X, deltaX, RawMouseDeviceName, timestamp, timestampMicroseconds);
        }

        if (deltaY is not 0)
        {
            EmitMove(InputEventCode.REL_Y, deltaY, RawMouseDeviceName, timestamp, timestampMicroseconds);
        }

        EmitSync(RawMouseDeviceName, timestamp, timestampMicroseconds);
    }

    private void HandleMouseMove(int currentX, int currentY)
    {
        bool hadPreviousPosition = _hasLastPosition;
        int previousX = _lastX;
        int previousY = _lastY;
        SetCursorPosition(currentX, currentY);

        if (!hadPreviousPosition && !useAbsoluteCoordinates)
        {
            return;
        }

        if (hadPreviousPosition && currentX == previousX && currentY == previousY)
        {
            return;
        }

        var movement = WindowsInputEventPolicy.ResolveMouseMovement(
            useAbsoluteCoordinates,
            currentX,
            currentY,
            previousX,
            previousY);
        long timestamp = CaptureTimestamps.UnixMilliseconds();
        long timestampMicroseconds = CaptureTimestamps.MonotonicMicroseconds();

        if (useAbsoluteCoordinates || movement.XValue is not 0)
        {
            EmitMove(movement.XCode, movement.XValue, MouseDeviceName, timestamp, timestampMicroseconds);
        }

        if (useAbsoluteCoordinates || movement.YValue is not 0)
        {
            EmitMove(movement.YCode, movement.YValue, MouseDeviceName, timestamp, timestampMicroseconds);
        }

        EmitSync(MouseDeviceName, timestamp, timestampMicroseconds);
    }

    private void HandleMouseButtonOrScroll(ushort evdevCode, int value, ushort type)
    {
        if (type is InputEventCode.EV_REL)
        {
            value = ToNotches(evdevCode, value);
            if (value is 0)
            {
                return;
            }
        }

        emit(new CapturedInputEvent
        {
            Type = WindowsInputEventPolicy.ClassifyMouseEvent(type, evdevCode),
            Code = evdevCode,
            Value = value,
            Timestamp = CaptureTimestamps.UnixMilliseconds(),
            TimestampMicroseconds = CaptureTimestamps.MonotonicMicroseconds(),
            DeviceName = MouseDeviceName,
        });
    }

    private int ToNotches(ushort wheelCode, int wheelDelta) => wheelCode is InputEventCode.REL_HWHEEL
        ? _horizontalScroll.Add(wheelDelta, User32.WheelDelta)
        : _verticalScroll.Add(wheelDelta, User32.WheelDelta);

    private void EmitMove(int code, int value, string deviceName, long timestamp, long timestampMicroseconds) =>
        emit(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = code,
            Value = value,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
            DeviceName = deviceName,
        });

    private void EmitSync(string deviceName, long timestamp, long timestampMicroseconds) =>
        emit(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
            DeviceName = deviceName,
        });
}
