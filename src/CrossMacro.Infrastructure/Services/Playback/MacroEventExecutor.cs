
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
    IPlaybackCoordinator coordinator) : IEventExecutor
{
    private readonly IInputSimulator _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    private readonly IButtonStateTracker _buttonTracker = buttonTracker ?? throw new ArgumentNullException(nameof(buttonTracker));
    private readonly IKeyStateTracker _keyTracker = keyTracker ?? throw new ArgumentNullException(nameof(keyTracker));
    private readonly IPlaybackMouseButtonMapper _buttonMapper = buttonMapper ?? throw new ArgumentNullException(nameof(buttonMapper));
    private readonly IPlaybackCoordinator _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly bool _usesZeroBasedScreenBounds = simulator is IInputSimulatorAbsoluteBounds
    {
        UsesZeroBasedScreenBounds: true,
    };

    private bool _disposed;
    private ScreenRect? _desktopBounds;
    private readonly bool _supportsAbsoluteCoordinates = simulator is not IInputSimulatorCapabilities capabilities || capabilities.SupportsAbsoluteCoordinates;

    public bool IsMouseButtonPressed => _buttonTracker.IsAnyPressed;

    public void Initialize(int screenWidth, int screenHeight)
    {
        _desktopBounds = screenWidth > 0 && screenHeight > 0
            ? new ScreenRect(0, 0, screenWidth, screenHeight)
            : null;
    }

    public void Initialize(ScreenRect? desktopBounds)
    {
        _desktopBounds = desktopBounds;
    }

    public void MoveAbsolute(int x, int y)
    {
        var target = ClampLogicalPosition(x, y);
        MoveLogicalTarget(target);
    }

    public void MoveRelative(int dx, int dy)
    {
        _simulator.MoveRelative(dx, dy);
        _coordinator.InvalidatePosition(movementMayBePending: true);
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
    public void Execute(
        MacroEvent ev,
        MouseCoordinateMode? coordinateMode,
        MouseCoordinateSpace? coordinateSpace = null)
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
                    ExecuteRelativeMove(ev.X, ev.Y, coordinateSpace);
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
                ExecuteMouseMove(ev, coordinateMode, coordinateSpace);
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

    private void ExecuteMouseMove(
        MacroEvent ev,
        MouseCoordinateMode? coordinateMode,
        MouseCoordinateSpace? coordinateSpace)
    {
        if (coordinateMode is MouseCoordinateMode.Absolute)
        {
            if (!_supportsAbsoluteCoordinates)
            {
                MoveRecordedAbsolute(ev.X, ev.Y);
                return;
            }

            MoveLogicalTarget(ClampLogicalPosition(ev.X, ev.Y));
        }
        else if (coordinateMode is MouseCoordinateMode.Relative)
        {
            ExecuteRelativeMove(ev.X, ev.Y, coordinateSpace);
        }
    }

    private void ExecuteRelativeMove(
        int deltaX,
        int deltaY,
        MouseCoordinateSpace? coordinateSpace)
    {
        if (deltaX is 0 && deltaY is 0)
        {
            return;
        }

        if (coordinateSpace is MouseCoordinateSpace.LogicalDesktop)
        {
            if (!_supportsAbsoluteCoordinates)
            {
                throw new AbsolutePlaybackUnsupportedException(_simulator.ProviderName);
            }

            if (!_coordinator.HasKnownPosition)
            {
                throw new LogicalRelativePositionUnavailableException();
            }

            var target = ClampLogicalPosition(
                (long)_coordinator.CurrentX + deltaX,
                (long)_coordinator.CurrentY + deltaY);
            MoveLogicalTarget(target);
            return;
        }

        _simulator.MoveRelative(deltaX, deltaY);
        _coordinator.InvalidatePosition(movementMayBePending: true);
    }

    private void MoveRecordedAbsolute(int targetX, int targetY)
    {
        if (!_supportsAbsoluteCoordinates)
        {
            throw new AbsolutePlaybackUnsupportedException(_simulator.ProviderName);
        }

        var target = ClampLogicalPosition(targetX, targetY);
        MoveLogicalTarget(target);
    }

    private void MoveLogicalTarget((int X, int Y) target)
    {
        SendAbsolute(target);
        _coordinator.UpdatePosition(target.X, target.Y);
    }

    private (int X, int Y) ClampLogicalPosition(long x, long y)
    {
        if (!_usesZeroBasedScreenBounds || _desktopBounds is not { } bounds)
        {
            return ((int)Math.Clamp(x, int.MinValue, int.MaxValue), (int)Math.Clamp(y, int.MinValue, int.MaxValue));
        }

        return ((int)Math.Clamp(x, bounds.X, bounds.Right - 1L), (int)Math.Clamp(y, bounds.Y, bounds.Bottom - 1L));
    }

    private void SendAbsolute((int X, int Y) logicalPosition)
    {
        if (_usesZeroBasedScreenBounds && _desktopBounds is { } bounds)
        {
            _simulator.MoveAbsolute(logicalPosition.X - bounds.X, logicalPosition.Y - bounds.Y);
            return;
        }

        _simulator.MoveAbsolute(logicalPosition.X, logicalPosition.Y);
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
