using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Thread-safe implementation of IButtonStateTracker.
/// Uses ConcurrentDictionary for lock-free state tracking.
/// </summary>
public class ButtonStateTracker : IButtonStateTracker
{
    private readonly ConcurrentDictionary<ushort, byte> _pressedButtons = new();

    public bool IsAnyPressed => !_pressedButtons.IsEmpty;
    
    public IReadOnlyCollection<ushort> PressedButtons => _pressedButtons.Keys.ToArray();

    public void Press(ushort button)
    {
        _pressedButtons.TryAdd(button, 0);
    }

    public void Release(ushort button)
    {
        _pressedButtons.TryRemove(button, out _);
    }

    public void Clear()
    {
        _pressedButtons.Clear();
    }

    public void ReleaseAll(IInputSimulator simulator)
    {
        if (_pressedButtons.IsEmpty)
            return;

        Log.Information("[ButtonStateTracker] Releasing {Count} pressed buttons", _pressedButtons.Count);
        
        var buttonsToRelease = _pressedButtons.Keys.ToArray();
        _pressedButtons.Clear();

        foreach (var button in buttonsToRelease)
        {
            try
            {
                simulator.MouseButton(button, false);
                Log.Debug("[ButtonStateTracker] Released button: {Button}", button);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ButtonStateTracker] Failed to release button: {Button}", button);
            }
        }

        // Failsafe: ensure common buttons are released
        try
        {
            simulator.MouseButton(MouseButtonCode.Left, false);
            simulator.MouseButton(MouseButtonCode.Right, false);
            simulator.MouseButton(MouseButtonCode.Middle, false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ButtonStateTracker] Failsafe release failed");
        }
    }

    public void RestoreAll(IInputSimulator simulator, IEnumerable<ushort> buttons)
    {
        foreach (var button in buttons)
        {
            try
            {
                simulator.MouseButton(button, true);
                _pressedButtons.TryAdd(button, 0);
                Log.Debug("[ButtonStateTracker] Re-pressed button: {Button}", button);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ButtonStateTracker] Failed to re-press button: {Button}", button);
            }
        }
    }
}
