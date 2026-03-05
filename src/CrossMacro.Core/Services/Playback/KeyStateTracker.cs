using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Thread-safe implementation of IKeyStateTracker.
/// Uses ConcurrentDictionary for lock-free state tracking.
/// </summary>
public class KeyStateTracker : IKeyStateTracker
{
    private readonly ConcurrentDictionary<int, byte> _pressedKeys = new();

    public IReadOnlyCollection<int> PressedKeys => _pressedKeys.Keys.ToArray();

    public void Press(int keyCode)
    {
        _pressedKeys.TryAdd(keyCode, 0);
    }

    public void Release(int keyCode)
    {
        _pressedKeys.TryRemove(keyCode, out _);
    }

    public void Clear()
    {
        _pressedKeys.Clear();
    }

    public void ReleaseAll(IInputSimulator simulator)
    {
        if (_pressedKeys.IsEmpty)
            return;

        Log.Information("[KeyStateTracker] Releasing {Count} pressed keys", _pressedKeys.Count);

        var keysToRelease = _pressedKeys.Keys.ToArray();
        _pressedKeys.Clear();

        foreach (var keyCode in keysToRelease)
        {
            try
            {
                simulator.KeyPress(keyCode, false);
                Log.Debug("[KeyStateTracker] Released key: {KeyCode}", keyCode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[KeyStateTracker] Failed to release key: {KeyCode}", keyCode);
            }
        }
    }

    public void RestoreAll(IInputSimulator simulator, IEnumerable<int> keys)
    {
        foreach (var keyCode in keys)
        {
            try
            {
                simulator.KeyPress(keyCode, true);
                _pressedKeys.TryAdd(keyCode, 0);
                Log.Debug("[KeyStateTracker] Re-pressed key: {KeyCode}", keyCode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[KeyStateTracker] Failed to re-press key: {KeyCode}", keyCode);
            }
        }
    }
}
