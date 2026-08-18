
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Thread-safe implementation of IKeyStateTracker.
/// Uses ConcurrentDictionary for lock-free state tracking.
/// </summary>
public class KeyStateTracker : IKeyStateTracker
{
    private readonly HashSet<int> _pressedKeys = [];
    private readonly Lock _lock = new();
    public IReadOnlyCollection<int> PressedKeys { get; private set; } = new ReadOnlyCollection<int>([]);

    public void Press(int keyCode)
    {
        using (_lock.EnterScope())
        {
            if (_pressedKeys.Add(keyCode))
            {
                RefreshSnapshot();
            }
        }
    }

    public void Release(int keyCode)
    {
        using (_lock.EnterScope())
        {
            if (_pressedKeys.Remove(keyCode))
            {
                RefreshSnapshot();
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            if (_pressedKeys.Count > 0)
            {
                _pressedKeys.Clear();
                RefreshSnapshot();
            }
        }
    }

    public void ReleaseAll(IInputSimulator simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        int[] keysToRelease;
        using (_lock.EnterScope())
        {
            if (_pressedKeys.Count is 0)
            {
                return;
            }

            Log.Information("[KeyStateTracker] Releasing {Count} pressed keys", _pressedKeys.Count);
            keysToRelease = [.. _pressedKeys];
            _pressedKeys.Clear();
            RefreshSnapshot();
        }

        foreach (var keyCode in keysToRelease)
        {
            try
            {
                simulator.KeyPress(keyCode, pressed: false);
                Log.Debug("[KeyStateTracker] Released key: {KeyCode}", keyCode);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[KeyStateTracker] Failed to release key: {KeyCode}", keyCode);
            }
        }
    }

    public void RestoreAll(IInputSimulator simulator, IEnumerable<int> keys)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var keyCode in keys)
        {
            try
            {
                simulator.KeyPress(keyCode, pressed: true);
                using (_lock.EnterScope())
                {
                    if (_pressedKeys.Add(keyCode))
                    {
                        RefreshSnapshot();
                    }
                }
                Log.Debug("[KeyStateTracker] Re-pressed key: {KeyCode}", keyCode);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[KeyStateTracker] Failed to re-press key: {KeyCode}", keyCode);
            }
        }
    }

    private void RefreshSnapshot()
    {
        PressedKeys = new ReadOnlyCollection<int>([.. _pressedKeys]);
    }
}
