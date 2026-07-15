namespace CrossMacro.Core.Models;

/// <summary>
/// Types of mouse events
/// </summary>
public enum EventType
{
    /// <summary>
    /// No event / default state
    /// </summary>
    None = 0,

    /// <summary>
    /// Mouse button pressed
    /// </summary>
    ButtonPress,

    /// <summary>
    /// Mouse button released
    /// </summary>
    ButtonRelease,

    /// <summary>
    /// Mouse moved by coordinates or deltas interpreted by the effective coordinate mode.
    /// </summary>
    MouseMove,

    /// <summary>
    /// Mouse click (press + release)
    /// </summary>
    Click,

    /// <summary>
    /// Keyboard key pressed
    /// </summary>
    KeyPress,

    /// <summary>
    /// Keyboard key released
    /// </summary>
    KeyRelease,
}
