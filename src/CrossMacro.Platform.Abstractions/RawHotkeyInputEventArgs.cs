namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Event args for raw input events forwarded from GlobalHotkeyService
/// </summary>
public class RawHotkeyInputEventArgs(
    int keyCode,
    IReadOnlySet<int> pressedModifiers,
    string hotkeyString) : EventArgs
{
    /// <summary>
    /// The key code that was pressed
    /// </summary>
    public int KeyCode { get; } = keyCode;

    /// <summary>
    /// Set of currently pressed modifier key codes
    /// </summary>
    public IReadOnlySet<int> PressedModifiers { get; } = pressedModifiers;

    /// <summary>
    /// The full hotkey string (e.g., "Ctrl+Shift+P")
    /// </summary>
    public string HotkeyString { get; } = hotkeyString;
}
