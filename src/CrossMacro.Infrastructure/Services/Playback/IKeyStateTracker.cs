
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Tracks pressed keyboard key state for playback.
/// Enables pause/resume with state preservation.
/// </summary>
public interface IKeyStateTracker
{
    /// <summary>
    /// Record a key press
    /// </summary>
    public void Press(int keyCode);

    /// <summary>
    /// Record a key release
    /// </summary>
    public void Release(int keyCode);

    /// <summary>
    /// Get all currently pressed keys
    /// </summary>
    public IReadOnlyCollection<int> PressedKeys { get; }

    /// <summary>
    /// Release all tracked keys via simulator and clear state
    /// </summary>
    public void ReleaseAll(IInputSimulator simulator);

    /// <summary>
    /// Restore all previously pressed keys via simulator
    /// </summary>
    public void RestoreAll(IInputSimulator simulator, IEnumerable<int> keys);

    /// <summary>
    /// Clear all tracking state without sending any events
    /// </summary>
    public void Clear();
}
