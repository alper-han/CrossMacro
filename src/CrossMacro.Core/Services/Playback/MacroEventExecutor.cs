using System;
using CrossMacro.Core.Models;
using Serilog;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Executes macro events using composed components.
/// Follows SRP by delegating to specialized trackers and mappers.
/// </summary>
public class MacroEventExecutor : IEventExecutor
{
    private readonly IInputSimulator _simulator;
    private readonly IButtonStateTracker _buttonTracker;
    private readonly IKeyStateTracker _keyTracker;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;
    private readonly IPlaybackCoordinator _coordinator;
    
    private int _screenWidth;
    private int _screenHeight;
    private bool _disposed;

    public MacroEventExecutor(
        IInputSimulator simulator,
        IButtonStateTracker buttonTracker,
        IKeyStateTracker keyTracker,
        IPlaybackMouseButtonMapper buttonMapper,
        IPlaybackCoordinator coordinator)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _buttonTracker = buttonTracker ?? throw new ArgumentNullException(nameof(buttonTracker));
        _keyTracker = keyTracker ?? throw new ArgumentNullException(nameof(keyTracker));
        _buttonMapper = buttonMapper ?? throw new ArgumentNullException(nameof(buttonMapper));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public bool IsMouseButtonPressed => _buttonTracker.IsAnyPressed;

    public void Initialize(int screenWidth, int screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
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
            _buttonTracker.Press(button);
        else
            _buttonTracker.Release(button);
    }

    public void EmitScroll(int value)
    {
        _simulator.Scroll(value);
    }

    public void EmitKey(int keyCode, bool pressed)
    {
        _simulator.KeyPress(keyCode, pressed);
        
        if (pressed)
            _keyTracker.Press(keyCode);
        else
            _keyTracker.Release(keyCode);
    }

    public void ReleaseAll()
    {
        _buttonTracker.ReleaseAll(_simulator);
        _keyTracker.ReleaseAll(_simulator);
    }

    /// <summary>
    /// Execute a single macro event
    /// </summary>
    public void Execute(MacroEvent ev, bool isRecordedAbsolute)
    {
        // Handle implicit movement for mouse button events (not keyboard, not scroll)
        if (ev.Type is EventType.ButtonPress or EventType.ButtonRelease or EventType.Click)
        {
            // Skip scroll events - they don't have meaningful coordinates
            bool isScroll = ev.Button is MouseButton.ScrollUp or MouseButton.ScrollDown
                or MouseButton.ScrollLeft or MouseButton.ScrollRight;

            if (!isScroll)
            {
                if (isRecordedAbsolute)
                {
                    // Absolute mode: calculate delta and move
                    int dx = ev.X - _coordinator.CurrentX;
                    int dy = ev.Y - _coordinator.CurrentY;
                    if (dx != 0 || dy != 0)
                    {
                        _simulator.MoveRelative(dx, dy);
                        _coordinator.UpdatePosition(ev.X, ev.Y);
                    }
                }
                else if (ev.X != 0 || ev.Y != 0)
                {
                    // Relative mode: use delta directly
                    MoveRelative(ev.X, ev.Y);
                }
            }
        }

        if (ev.Type != EventType.MouseMove)
        {
            Log.Debug("[MacroEventExecutor] Executing {Type}", ev.Type);
        }

        switch (ev.Type)
        {
            case EventType.ButtonPress:
                LogButtonEvent("ButtonPress", ev);
                var pressButton = (ushort)_buttonMapper.Map(ev.Button);
                EmitButton(pressButton, true);
                break;

            case EventType.ButtonRelease:
                LogButtonEvent("ButtonRelease", ev);
                var releaseButton = (ushort)_buttonMapper.Map(ev.Button);
                EmitButton(releaseButton, false);
                break;

            case EventType.MouseMove:
                ExecuteMouseMove(ev, isRecordedAbsolute);
                break;

            case EventType.Click:
                ExecuteClick(ev);
                break;

            case EventType.KeyPress:
                LogKeyEvent("KeyPress", ev.KeyCode);
                EmitKey(ev.KeyCode, true);
                break;

            case EventType.KeyRelease:
                LogKeyEvent("KeyRelease", ev.KeyCode);
                EmitKey(ev.KeyCode, false);
                break;
        }
    }

    private void ExecuteMouseMove(MacroEvent ev, bool isRecordedAbsolute)
    {
        if (isRecordedAbsolute)
        {
            if (_buttonTracker.IsAnyPressed)
            {
                // Button pressed - use relative to avoid Wayland drawing issues
                int dx = ev.X - _coordinator.CurrentX;
                int dy = ev.Y - _coordinator.CurrentY;
                if (dx != 0 || dy != 0)
                {
                    _simulator.MoveRelative(dx, dy);
                }
            }
            else
            {
                // Button not pressed - use absolute for drift correction
                _simulator.MoveAbsolute(ev.X, ev.Y);
            }
            _coordinator.UpdatePosition(ev.X, ev.Y);
        }
        else
        {
            MoveRelative(ev.X, ev.Y);
        }
    }

    private void ExecuteClick(MacroEvent ev)
    {
        switch (ev.Button)
        {
            case MouseButton.ScrollUp:
                LogScroll("UP");
                _simulator.Scroll(1);
                break;
                
            case MouseButton.ScrollDown:
                LogScroll("DOWN");
                _simulator.Scroll(-1);
                break;
                
            case MouseButton.ScrollLeft:
                LogScroll("LEFT");
                _simulator.Scroll(-1, true);
                break;
                
            case MouseButton.ScrollRight:
                LogScroll("RIGHT");
                _simulator.Scroll(1, true);
                break;
                
            default:
                LogClickEvent(ev);
                var clickButton = (ushort)_buttonMapper.Map(ev.Button);
                _simulator.MouseButton(clickButton, true);
                _simulator.MouseButton(clickButton, false);
                break;
        }
    }

    private static void LogButtonEvent(string action, MacroEvent ev)
    {
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
        {
            Log.Information("[MacroEventExecutor] {Action}: {Button} at ({X}, {Y})", action, ev.Button, ev.X, ev.Y);
        }
    }

    private static void LogKeyEvent(string action, int keyCode)
    {
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
        {
            Log.Information("[MacroEventExecutor] {Action}: KeyCode={KeyCode}", action, keyCode);
        }
    }

    private static void LogScroll(string direction)
    {
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
        {
            Log.Information("[MacroEventExecutor] SCROLL {Direction}", direction);
        }
    }

    private static void LogClickEvent(MacroEvent ev)
    {
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
        {
            Log.Information("[MacroEventExecutor] CLICK: {Button} at ({X}, {Y})", ev.Button, ev.X, ev.Y);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseAll();
        _disposed = true;
    }
}
