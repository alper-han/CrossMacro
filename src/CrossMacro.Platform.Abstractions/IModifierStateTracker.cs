namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Tracks the state of modifier keys (Ctrl, Shift, Alt, etc.)
/// </summary>
public interface IModifierStateTracker
{
    /// <summary>
    /// Records a key press event.
    /// </summary>
    /// <param name="keyCode">The key code that was pressed</param>
    void OnKeyPressed(int keyCode);

    /// <summary>
    /// Records a key release event.
    /// </summary>
    /// <param name="keyCode">The key code that was released</param>
    void OnKeyReleased(int keyCode);

    /// <summary>
    /// Gets the set of currently pressed modifier key codes.
    /// </summary>
    IReadOnlySet<int> CurrentModifiers { get; }

    /// <summary>
    /// Clears all tracked modifier state.
    /// </summary>
    void Clear();

    /// <summary>
    /// Checks if any modifiers are currently pressed.
    /// </summary>
    bool HasModifiers { get; }
}
