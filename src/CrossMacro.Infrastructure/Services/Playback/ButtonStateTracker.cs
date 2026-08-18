
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Thread-safe implementation of IButtonStateTracker.
/// Uses ConcurrentDictionary for lock-free state tracking.
/// </summary>
public class ButtonStateTracker : IButtonStateTracker
{
    private readonly HashSet<ushort> _pressedButtons = [];
    private readonly Lock _lock = new();
    public IReadOnlyCollection<ushort> PressedButtons { get; private set; } = new ReadOnlyCollection<ushort>([]);

    public bool IsAnyPressed
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _pressedButtons.Count > 0;
            }
        }
    }

    public void Press(ushort button)
    {
        using (_lock.EnterScope())
        {
            if (_pressedButtons.Add(button))
            {
                RefreshSnapshot();
            }
        }
    }

    public void Release(ushort button)
    {
        using (_lock.EnterScope())
        {
            if (_pressedButtons.Remove(button))
            {
                RefreshSnapshot();
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            if (_pressedButtons.Count > 0)
            {
                _pressedButtons.Clear();
                RefreshSnapshot();
            }
        }
    }

    public void ReleaseAll(IInputSimulator simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ushort[] buttonsToRelease;
        using (_lock.EnterScope())
        {
            if (_pressedButtons.Count is 0)
            {
                return;
            }

            Log.Information("[ButtonStateTracker] Releasing {Count} pressed buttons", _pressedButtons.Count);
            buttonsToRelease = [.. _pressedButtons];
            _pressedButtons.Clear();
            RefreshSnapshot();
        }

        foreach (var button in buttonsToRelease)
        {
            try
            {
                simulator.MouseButton(button, pressed: false);
                Log.Debug("[ButtonStateTracker] Released button: {Button}", button);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[ButtonStateTracker] Failed to release button: {Button}", button);
            }
        }

        // Failsafe: ensure common buttons are released
        try
        {
            simulator.MouseButton(MouseButtonCode.Left, pressed: false);
            simulator.MouseButton(MouseButtonCode.Right, pressed: false);
            simulator.MouseButton(MouseButtonCode.Middle, pressed: false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[ButtonStateTracker] Failsafe release failed");
        }
    }

    public void RestoreAll(IInputSimulator simulator, IEnumerable<ushort> buttons)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(buttons);
        foreach (var button in buttons)
        {
            try
            {
                simulator.MouseButton(button, pressed: true);
                using (_lock.EnterScope())
                {
                    if (_pressedButtons.Add(button))
                    {
                        RefreshSnapshot();
                    }
                }
                Log.Debug("[ButtonStateTracker] Re-pressed button: {Button}", button);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[ButtonStateTracker] Failed to re-press button: {Button}", button);
            }
        }
    }

    private void RefreshSnapshot()
    {
        PressedButtons = new ReadOnlyCollection<ushort>([.. _pressedButtons]);
    }
}
