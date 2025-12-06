using System;
using System.Collections.Concurrent;
using System.Linq;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Interface for executing input events (mouse, keyboard)
/// </summary>
public interface IEventExecutor : IDisposable
{
    /// <summary>
    /// Initialize the executor (create virtual device)
    /// </summary>
    void Initialize(int screenWidth, int screenHeight);
    
    /// <summary>
    /// Move mouse to absolute position
    /// </summary>
    void MoveAbsolute(int x, int y);
    
    /// <summary>
    /// Move mouse by relative delta
    /// </summary>
    void MoveRelative(int dx, int dy);
    
    /// <summary>
    /// Press or release mouse button
    /// </summary>
    void EmitButton(ushort button, bool pressed);
    
    /// <summary>
    /// Emit mouse scroll
    /// </summary>
    void EmitScroll(int value);
    
    /// <summary>
    /// Press or release keyboard key
    /// </summary>
    void EmitKey(int keyCode, bool pressed);
    
    /// <summary>
    /// Release all pressed inputs (safety)
    /// </summary>
    void ReleaseAll();
    
    /// <summary>
    /// Whether any mouse button is currently pressed
    /// </summary>
    bool IsMouseButtonPressed { get; }
}

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
    }
    
    public void MoveRelative(int dx, int dy)
    {
        _device?.Move(dx, dy);
    }
    
    public void EmitButton(ushort button, bool pressed)
    {
        if (_device == null) return;
        
        _device.EmitButton(button, pressed);
        
        if (pressed)
            _pressedButtons.TryAdd(button, 0);
        else
            _pressedButtons.TryRemove(button, out _);
    }
    
    public void EmitScroll(int value)
    {
        if (_device == null) return;
        
        _device.SendEvent(UInputNative.EV_REL, UInputNative.REL_WHEEL, value);
        _device.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }
    
    public void EmitKey(int keyCode, bool pressed)
    {
        if (_device == null) return;
        
        _device.EmitKey(keyCode, pressed);
        
        if (pressed)
            _pressedKeys.TryAdd(keyCode, 0);
        else
            _pressedKeys.TryRemove(keyCode, out _);
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
        catch { }
        
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
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ReleaseAll();
        _device?.Dispose();
        _device = null;
    }
}
