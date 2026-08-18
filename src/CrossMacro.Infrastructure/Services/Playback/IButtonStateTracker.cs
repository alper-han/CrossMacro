
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Tracks pressed mouse button state for playback.
/// Enables pause/resume with state preservation.
/// </summary>
public interface IButtonStateTracker
{
    /// <summary>
    /// Record a button press
    /// </summary>
    public void Press(ushort button);

    /// <summary>
    /// Record a button release
    /// </summary>
    public void Release(ushort button);

    /// <summary>
    /// Whether any button is currently pressed
    /// </summary>
    public bool IsAnyPressed { get; }

    /// <summary>
    /// Get all currently pressed buttons
    /// </summary>
    public IReadOnlyCollection<ushort> PressedButtons { get; }

    /// <summary>
    /// Release all tracked buttons via simulator and clear state
    /// </summary>
    public void ReleaseAll(IInputSimulator simulator);

    /// <summary>
    /// Restore all previously pressed buttons via simulator
    /// </summary>
    public void RestoreAll(IInputSimulator simulator, IEnumerable<ushort> buttons);

    /// <summary>
    /// Clear all tracking state without sending any events
    /// </summary>
    public void Clear();
}
