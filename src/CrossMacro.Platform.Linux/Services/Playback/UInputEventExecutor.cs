
namespace CrossMacro.Platform.Linux.Services.Playback;

/// <summary>
/// UInput-based event executor implementation
/// Single Responsibility: Executes input events through uinput virtual device
/// </summary>
public sealed class UInputEventExecutor : IEventExecutor
{
    private UInputDevice? Device { get; set; }
    private bool _disposed;

    private readonly ConcurrentDictionary<ushort, byte> _pressedButtons = new();
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();
    private bool _hasKnownPosition;
    private int _currentX;
    private int _currentY;

    public bool IsMouseButtonPressed => !_pressedButtons.IsEmpty;

    public void Initialize(int screenWidth, int screenHeight)
    {
        Device?.Dispose();
        Device = new UInputDevice(screenWidth, screenHeight);
        Device.CreateVirtualInputDevice();

        _pressedButtons.Clear();
        _pressedKeys.Clear();
        _hasKnownPosition = false;

        Log.Information("[UInputEventExecutor] Virtual device created ({Width}x{Height})", screenWidth, screenHeight);
    }

    public void MoveAbsolute(int x, int y)
    {
        Device?.MoveAbsolute(x, y);
        _currentX = x;
        _currentY = y;
        _hasKnownPosition = true;
        Log.Debug("[UInputEventExecutor] MoveAbsolute: X={X} Y={Y}", x, y);
    }

    public void MoveRelative(int dx, int dy)
    {
        Device?.Move(dx, dy);
        _hasKnownPosition = false;
        Log.Debug("[UInputEventExecutor] MoveRelative: dX={dX} dY={dY}", dx, dy);
    }

    public void EmitButton(ushort button, bool pressed)
    {
        if (Device is null)
        {
            return;
        }

        Device.EmitButton(button, pressed);

        if (pressed)
        {
            _ = _pressedButtons.TryAdd(button, 0);
        }
        else
        {
            _ = _pressedButtons.TryRemove(button, out _);
        }

        Log.Debug("[UInputEventExecutor] Button: {Button} State={State}", button, pressed ? "Pressed" : "Released");
    }

    public void EmitScroll(int value)
    {
        if (Device is null)
        {
            return;
        }

        Device.SendEvent(UInputNative.EV_REL, UInputNative.REL_WHEEL, value);
        Device.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);

        Log.Debug("[UInputEventExecutor] Scroll: {Value}", value > 0 ? "Up" : "Down");
    }

    public void EmitKey(int code, bool pressed)
    {
        if (Device is null)
        {
            return;
        }

        Device.EmitKey(code, pressed);

        if (pressed)
        {
            _ = _pressedKeys.TryAdd(code, 0);
        }
        else
        {
            _ = _pressedKeys.TryRemove(code, out _);
        }

        Log.Debug("[UInputEventExecutor] Key: {KeyCode} State={State}", code, pressed ? "Pressed" : "Released");
    }

    public void ReleaseAll()
    {
        if (Device is null)
        {
            return;
        }

        // Release all tracked buttons
        var buttonsToRelease = _pressedButtons.Keys.ToArray();
        _pressedButtons.Clear();

        foreach (var button in buttonsToRelease)
        {
            try
            {
                Device.EmitButton(button, pressed: false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[UInputEventExecutor] Failed to release button {Button}", button);
            }
        }

        // Failsafe: release common buttons
        try
        {
            Device.EmitButton(UInputNative.BTN_LEFT, pressed: false);
            Device.EmitButton(UInputNative.BTN_RIGHT, pressed: false);
            Device.EmitButton(UInputNative.BTN_MIDDLE, pressed: false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[UInputEventExecutor] Failsafe button release failed");
        }

        // Release all tracked keys
        var keysToRelease = _pressedKeys.Keys.ToArray();
        _pressedKeys.Clear();

        foreach (var keyCode in keysToRelease)
        {
            try
            {
                Device.EmitKey(keyCode, pressed: false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[UInputEventExecutor] Failed to release key {KeyCode}", keyCode);
            }
        }

        Log.Debug("[UInputEventExecutor] Released all inputs");
    }

    public void Execute(
        MacroEvent ev,
        MouseCoordinateMode? coordinateMode,
        MouseCoordinateSpace? coordinateSpace = null)
    {
        // Handle implicit movement for mouse button events (not keyboard)
        if (ev.Type is EventType.ButtonPress or EventType.ButtonRelease or EventType.Click)
        {
            bool isScroll = MacroPositionSemantics.IsScrollButton(ev.Button);
            if (!isScroll && !ev.UseCurrentPosition && coordinateMode is MouseCoordinateMode.Absolute)
            {
                MoveAbsolute(ev.X, ev.Y);
            }
            else if (!isScroll && !ev.UseCurrentPosition && coordinateMode is MouseCoordinateMode.Relative && (ev.X is not 0 || ev.Y is not 0))
            {
                ExecuteRelativeMove(ev.X, ev.Y, coordinateSpace);
            }
        }

        switch (ev.Type)
        {
            case EventType.ButtonPress:
                var pressButton = MapButton(ev.Button);
                EmitButton(pressButton, pressed: true);
                break;

            case EventType.ButtonRelease:
                var releaseButton = MapButton(ev.Button);
                EmitButton(releaseButton, pressed: false);
                break;

            case EventType.MouseMove:
                if (coordinateMode is MouseCoordinateMode.Absolute)
                {
                    MoveAbsolute(ev.X, ev.Y);
                }
                else if (coordinateMode is MouseCoordinateMode.Relative)
                {
                    ExecuteRelativeMove(ev.X, ev.Y, coordinateSpace);
                }

                break;

            case EventType.Click:
                ExecuteClick(ev);
                break;

            case EventType.KeyPress:
                EmitKey(ev.KeyCode, pressed: true);
                break;

            case EventType.KeyRelease:
                EmitKey(ev.KeyCode, pressed: false);
                break;
        }
    }

    private void ExecuteRelativeMove(int deltaX, int deltaY, MouseCoordinateSpace? coordinateSpace)
    {
        if (coordinateSpace is MouseCoordinateSpace.LogicalDesktop)
        {
            if (!_hasKnownPosition)
            {
                throw new LogicalRelativePositionUnavailableException();
            }

            MoveAbsolute(
                (int)Math.Clamp((long)_currentX + deltaX, int.MinValue, int.MaxValue),
                (int)Math.Clamp((long)_currentY + deltaY, int.MinValue, int.MaxValue));
            return;
        }

        MoveRelative(deltaX, deltaY);
    }

    private void ExecuteClick(MacroEvent ev)
    {
        switch (ev.Button)
        {
            case MacroMouseButton.ScrollUp:
                EmitScroll(1);
                break;
            case MacroMouseButton.ScrollDown:
                EmitScroll(-1);
                break;
            default:
                var button = MapButton(ev.Button);
                EmitButton(button, pressed: true);
                EmitButton(button, pressed: false);
                break;
        }
    }

    private static ushort MapButton(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => UInputNative.BTN_LEFT,
            MacroMouseButton.Right => UInputNative.BTN_RIGHT,
            MacroMouseButton.Middle => UInputNative.BTN_MIDDLE,
            MacroMouseButton.Side1 => UInputNative.BTN_SIDE,
            MacroMouseButton.Side2 => UInputNative.BTN_EXTRA,
            MacroMouseButton.None => UInputNative.BTN_LEFT,
            MacroMouseButton.ScrollUp => UInputNative.BTN_LEFT,
            MacroMouseButton.ScrollDown => UInputNative.BTN_LEFT,
            MacroMouseButton.ScrollLeft => UInputNative.BTN_LEFT,
            MacroMouseButton.ScrollRight => UInputNative.BTN_LEFT,
            _ => UInputNative.BTN_LEFT,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        ReleaseAll();
        Device?.Dispose();
        GC.SuppressFinalize(this);
    }
}
