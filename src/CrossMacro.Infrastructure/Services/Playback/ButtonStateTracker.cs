
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Thread-safe implementation of IButtonStateTracker.
/// </summary>
public class ButtonStateTracker : IButtonStateTracker
{
    private readonly PressedSet<ushort> _pressed = new();

    public IReadOnlyCollection<ushort> PressedButtons => _pressed.Snapshot;

    public bool IsAnyPressed => _pressed.IsAny;

    public void Press(ushort button) => _pressed.Add(button);

    public void Release(ushort button) => _pressed.Remove(button);

    public void Clear() => _pressed.Clear();

    public void ReleaseAll(IInputSimulator simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        var buttonsToRelease = _pressed.TakeAll();
        if (buttonsToRelease.Length is 0)
        {
            return;
        }

        Log.Information("[ButtonStateTracker] Releasing {Count} pressed buttons", buttonsToRelease.Length);
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
                _pressed.Add(button);
                Log.Debug("[ButtonStateTracker] Re-pressed button: {Button}", button);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[ButtonStateTracker] Failed to re-press button: {Button}", button);
            }
        }
    }
}
