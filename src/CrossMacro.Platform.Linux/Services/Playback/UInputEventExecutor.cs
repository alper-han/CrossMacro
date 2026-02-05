using System;
using System.Collections.Concurrent;
using System.Linq;
using CrossMacro.Platform.Linux.Native.UInput;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using Serilog;

namespace CrossMacro.Platform.Linux.Services.Playback;

/// <summary>
/// UInput-based event executor implementation
/// Single Responsibility: Executes input events through uinput virtual device
/// </summary>
public class UInputEventExecutor : IEventExecutor
{
    private UInputDevice? _device;
    private bool _disposed;
    
    private readonly ConcurrentDictionary<ushort, byte> _pressedButtons = new();
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();
    
    public bool IsMouseButtonPressed => !_pressedButtons.IsEmpty;
    
    public void Initialize(int screenWidth, int screenHeight)
    {
        _device?.Dispose();
        _device = new UInputDevice(screenWidth, screenHeight);
        _device.CreateVirtualInputDevice();
        
        _pressedButtons.Clear();
        _pressedKeys.Clear();
        
        Log.Information("[UInputEventExecutor] Virtual device created ({Width}x{Height})", screenWidth, screenHeight);
    }
    
    public void MoveAbsolute(int x, int y)
    {
        _device?.MoveAbsolute(x, y);
        Log.Debug("[UInputEventExecutor] MoveAbsolute: X={X} Y={Y}", x, y);
    }

    public void MoveRelative(int dx, int dy)
    {
        _device?.Move(dx, dy);
        Log.Debug("[UInputEventExecutor] MoveRelative: dX={dX} dY={dY}", dx, dy);
    }

    public void EmitButton(ushort button, bool pressed)
    {
        if (_device == null) return;

        _device.EmitButton(button, pressed);

        if (pressed)
            _pressedButtons.TryAdd(button, 0);
        else
            _pressedButtons.TryRemove(button, out _);

        Log.Debug("[UInputEventExecutor] Button: {Button} State={State}", button, pressed ? "Pressed" : "Released");
    }

    public void EmitScroll(int value)
    {
        if (_device == null) return;

        _device.SendEvent(UInputNative.EV_REL, UInputNative.REL_WHEEL, value);
        _device.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);

        Log.Debug("[UInputEventExecutor] Scroll: {Value}", value > 0 ? "Up" : "Down");
    }

    public void EmitKey(int keyCode, bool pressed)
    {
        if (_device == null) return;

        _device.EmitKey(keyCode, pressed);

        if (pressed)
            _pressedKeys.TryAdd(keyCode, 0);
        else
            _pressedKeys.TryRemove(keyCode, out _);

        Log.Debug("[UInputEventExecutor] Key: {KeyCode} State={State}", keyCode, pressed ? "Pressed" : "Released");
    }
    
    public void ReleaseAll()
    {
        if (_device == null) return;
        
        // Release all tracked buttons
        var buttonsToRelease = _pressedButtons.Keys.ToArray();
        _pressedButtons.Clear();
        
        foreach (var button in buttonsToRelease)
        {
            try
            {
                _device.EmitButton(button, false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UInputEventExecutor] Failed to release button {Button}", button);
            }
        }
        
        // Failsafe: release common buttons
        try
        {
            _device.EmitButton(UInputNative.BTN_LEFT, false);
            _device.EmitButton(UInputNative.BTN_RIGHT, false);
            _device.EmitButton(UInputNative.BTN_MIDDLE, false);
        }
        catch (Exception ex)
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
                _device.EmitKey(keyCode, false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UInputEventExecutor] Failed to release key {KeyCode}", keyCode);
            }
        }
        
        Log.Debug("[UInputEventExecutor] Released all inputs");
    }
    
    public void Execute(MacroEvent ev, bool isRecordedAbsolute)
    {
        // Handle implicit movement for mouse button events (not keyboard)
        if (ev.Type is EventType.ButtonPress or EventType.ButtonRelease or EventType.Click && !isRecordedAbsolute)
        {
            if (ev.X != 0 || ev.Y != 0)
            {
                MoveRelative(ev.X, ev.Y);
            }
        }

        switch (ev.Type)
        {
            case EventType.ButtonPress:
                var pressButton = MapButton(ev.Button);
                EmitButton(pressButton, true);
                break;

            case EventType.ButtonRelease:
                var releaseButton = MapButton(ev.Button);
                EmitButton(releaseButton, false);
                break;

            case EventType.MouseMove:
                if (isRecordedAbsolute)
                    MoveAbsolute(ev.X, ev.Y);
                else
                    MoveRelative(ev.X, ev.Y);
                break;

            case EventType.Click:
                ExecuteClick(ev);
                break;

            case EventType.KeyPress:
                EmitKey(ev.KeyCode, true);
                break;

            case EventType.KeyRelease:
                EmitKey(ev.KeyCode, false);
                break;
        }
    }
    
    private void ExecuteClick(MacroEvent ev)
    {
        switch (ev.Button)
        {
            case MouseButton.ScrollUp:
                EmitScroll(1);
                break;
            case MouseButton.ScrollDown:
                EmitScroll(-1);
                break;
            default:
                var button = MapButton(ev.Button);
                EmitButton(button, true);
                EmitButton(button, false);
                break;
        }
    }
    
    private static ushort MapButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => UInputNative.BTN_LEFT,
            MouseButton.Right => UInputNative.BTN_RIGHT,
            MouseButton.Middle => UInputNative.BTN_MIDDLE,
            MouseButton.Side1 => UInputNative.BTN_SIDE,
            MouseButton.Side2 => UInputNative.BTN_EXTRA,
            _ => UInputNative.BTN_LEFT
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ReleaseAll();
        _device?.Dispose();
        _device = null;
    }
}
