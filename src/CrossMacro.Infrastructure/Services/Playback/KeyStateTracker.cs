
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Thread-safe implementation of IKeyStateTracker.
/// </summary>
public class KeyStateTracker : IKeyStateTracker
{
    private readonly PressedSet<int> _pressed = new();

    public IReadOnlyCollection<int> PressedKeys => _pressed.Snapshot;

    public void Press(int keyCode) => _pressed.Add(keyCode);

    public void Release(int keyCode) => _pressed.Remove(keyCode);

    public void Clear() => _pressed.Clear();

    public void ReleaseAll(IInputSimulator simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        var keysToRelease = _pressed.TakeAll();
        if (keysToRelease.Length is 0)
        {
            return;
        }

        Log.Information("[KeyStateTracker] Releasing {Count} pressed keys", keysToRelease.Length);
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
                _pressed.Add(keyCode);
                Log.Debug("[KeyStateTracker] Re-pressed key: {KeyCode}", keyCode);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[KeyStateTracker] Failed to re-press key: {KeyCode}", keyCode);
            }
        }
    }
}
