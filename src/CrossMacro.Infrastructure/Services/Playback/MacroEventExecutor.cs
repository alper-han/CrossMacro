
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Executes macro events using composed components.
/// Follows SRP by delegating to specialized trackers and mappers.
/// </summary>
public sealed class MacroEventExecutor(
    IInputSimulator simulator,
    IButtonStateTracker buttonTracker,
    IKeyStateTracker keyTracker,
    IPlaybackMouseButtonMapper buttonMapper,
    IPlaybackCoordinator coordinator,
    bool useHybridAbsoluteDragMovement = true) : IEventExecutor
{
    private readonly IInputSimulator _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    private readonly IButtonStateTracker _buttonTracker = buttonTracker ?? throw new ArgumentNullException(nameof(buttonTracker));
    private readonly IKeyStateTracker _keyTracker = keyTracker ?? throw new ArgumentNullException(nameof(keyTracker));
    private readonly IPlaybackMouseButtonMapper _buttonMapper = buttonMapper ?? throw new ArgumentNullException(nameof(buttonMapper));
    private readonly IPlaybackCoordinator _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly bool _useHybridAbsoluteDragMovement = useHybridAbsoluteDragMovement;

    private bool _disposed;
    private readonly bool _supportsAbsoluteCoordinates = simulator is not IInputSimulatorCapabilities capabilities || capabilities.SupportsAbsoluteCoordinates;

    public bool IsMouseButtonPressed => _buttonTracker.IsAnyPressed;

    public void Initialize(int screenWidth, int screenHeight)
    {
        // Note: Simulator is already initialized by MacroPlayer.AcquireSimulatorAsync
    }

    public void MoveAbsolute(int x, int y)
    {
        _simulator.MoveAbsolute(x, y);
        _coordinator.UpdatePosition(x, y);
    }

    public void MoveRelative(int dx, int dy)
    {
        _simulator.MoveRelative(dx, dy);
        _coordinator.AddDelta(dx, dy);
    }

    public void EmitButton(ushort button, bool pressed)
    {
        _simulator.MouseButton(button, pressed);

        if (pressed)
        {
            _buttonTracker.Press(button);
        }
        else
        {
            _buttonTracker.Release(button);
        }
    }

    public void EmitScroll(int value)
    {
        _simulator.Scroll(value);
    }

    public void EmitKey(int code, bool pressed)
    {
        _simulator.KeyPress(code, pressed);

        if (pressed)
        {
            _keyTracker.Press(code);
        }
        else
        {
            _keyTracker.Release(code);
        }
    }

    public void ReleaseAll()
    {
        _buttonTracker.ReleaseAll(_simulator);
        _keyTracker.ReleaseAll(_simulator);
    }

    /// <summary>
    /// Execute a single macro event
    /// </summary>
    public void Execute(MacroEvent ev, MouseCoordinateMode? coordinateMode)
    {
        // Handle implicit movement for mouse button events (not keyboard, not scroll)
        if (ev.Type is EventType.ButtonPress or EventType.ButtonRelease or EventType.Click)
        {
            // Skip scroll events - they don't have meaningful coordinates
            bool isScroll = ev.Button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown
                or MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollRight;
            bool shouldResolveFromCurrentPosition = ev.UseCurrentPosition && !isScroll;

            if (!isScroll && !shouldResolveFromCurrentPosition)
            {
                if (coordinateMode is MouseCoordinateMode.Absolute)
                {
                    MoveRecordedAbsolute(ev.X, ev.Y);
                }
                else if (coordinateMode is MouseCoordinateMode.Relative && (ev.X is not 0 || ev.Y is not 0))
                {
                    // Relative mode: use delta directly
                    MoveRelative(ev.X, ev.Y);
                }
            }
        }

        if (ev.Type is not EventType.MouseMove)
        {
            Log.Debug("[MacroEventExecutor] Executing {Type}", ev.Type);
        }

        switch (ev.Type)
        {
            case EventType.ButtonPress:
                LogButtonEvent("ButtonPress", ev);
                var pressButton = (ushort)_buttonMapper.Map(ev.Button);
                EmitButton(pressButton, pressed: true);
                break;

            case EventType.ButtonRelease:
                LogButtonEvent("ButtonRelease", ev);
                var releaseButton = (ushort)_buttonMapper.Map(ev.Button);
                EmitButton(releaseButton, pressed: false);
                break;

            case EventType.MouseMove:
                ExecuteMouseMove(ev, coordinateMode);
                break;

            case EventType.Click:
                ExecuteClick(ev);
                break;

            case EventType.KeyPress:
                LogKeyEvent("KeyPress", ev.KeyCode);
                EmitKey(ev.KeyCode, pressed: true);
                break;

            case EventType.KeyRelease:
                LogKeyEvent("KeyRelease", ev.KeyCode);
                EmitKey(ev.KeyCode, pressed: false);
                break;
        }
    }

    private void ExecuteMouseMove(MacroEvent ev, MouseCoordinateMode? coordinateMode)
    {
        if (coordinateMode is MouseCoordinateMode.Absolute)
        {
            if (!_supportsAbsoluteCoordinates)
            {
                MoveRecordedAbsolute(ev.X, ev.Y);
                return;
            }

            if (_buttonTracker.IsAnyPressed && _useHybridAbsoluteDragMovement)
            {
                // Button pressed - use relative for smooth Wayland curves
                // First sync to previous position with absolute (drift correction)
                _simulator.MoveAbsolute(_coordinator.CurrentX, _coordinator.CurrentY);

                // Then apply relative delta for smooth curve
                int dx = ev.X - _coordinator.CurrentX;
                int dy = ev.Y - _coordinator.CurrentY;
                if (dx is not 0 || dy is not 0)
                {
                    _simulator.MoveRelative(dx, dy);
                }
            }
            else
            {
                // Default absolute path (used on non-Linux to avoid ABS+REL jitter while dragging)
                _simulator.MoveAbsolute(ev.X, ev.Y);
            }
            _coordinator.UpdatePosition(ev.X, ev.Y);
        }
        else if (coordinateMode is MouseCoordinateMode.Relative)
        {
            MoveRelative(ev.X, ev.Y);
        }
    }

    private void MoveRecordedAbsolute(int targetX, int targetY)
    {
        if (!_supportsAbsoluteCoordinates)
        {
            throw new AbsolutePlaybackUnsupportedException(_simulator.ProviderName);
        }

        _simulator.MoveAbsolute(targetX, targetY);
        _coordinator.UpdatePosition(targetX, targetY);
    }

    private void ExecuteClick(MacroEvent ev)
    {
        switch (ev.Button)
        {
            case MacroMouseButton.ScrollUp:
                LogScroll("UP");
                _simulator.Scroll(1);
                break;

            case MacroMouseButton.ScrollDown:
                LogScroll("DOWN");
                _simulator.Scroll(-1);
                break;

            case MacroMouseButton.ScrollLeft:
                LogScroll("LEFT");
                _simulator.Scroll(-1, isHorizontal: true);
                break;

            case MacroMouseButton.ScrollRight:
                LogScroll("RIGHT");
                _simulator.Scroll(1, isHorizontal: true);
                break;

            default:
                LogClickEvent(ev);
                var clickButton = (ushort)_buttonMapper.Map(ev.Button);
                _simulator.MouseButton(clickButton, pressed: true);
                _simulator.MouseButton(clickButton, pressed: false);
                break;
        }
    }

    private static void LogButtonEvent(string action, MacroEvent ev)
    {
        if (Log.IsEnabled(CoreLogLevel.Debug))
        {
            Log.Debug("[MacroEventExecutor] {Action}: {Button} at ({X}, {Y})", action, ev.Button, ev.X, ev.Y);
        }
    }

    private static void LogKeyEvent(string action, int keyCode)
    {
        if (Log.IsEnabled(CoreLogLevel.Debug))
        {
            Log.Debug("[MacroEventExecutor] {Action}: KeyCode={KeyCode}", action, keyCode);
        }
    }

    private static void LogScroll(string direction)
    {
        if (Log.IsEnabled(CoreLogLevel.Debug))
        {
            Log.Debug("[MacroEventExecutor] SCROLL {Direction}", direction);
        }
    }

    private static void LogClickEvent(MacroEvent ev)
    {
        if (Log.IsEnabled(CoreLogLevel.Debug))
        {
            Log.Debug("[MacroEventExecutor] CLICK: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseAll();
        _disposed = true;
    }
}
