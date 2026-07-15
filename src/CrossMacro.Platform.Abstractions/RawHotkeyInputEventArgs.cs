namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Event args for raw input events forwarded from GlobalHotkeyService
/// </summary>
public class RawHotkeyInputEventArgs : EventArgs
{
    /// <summary>
    /// The key code that was pressed
    /// </summary>
    public int KeyCode { get; }

    /// <summary>
    /// Set of currently pressed modifier key codes
    /// </summary>
    public IReadOnlySet<int> PressedModifiers { get; }

    /// <summary>
    /// The full hotkey string (e.g., "Ctrl+Shift+P")
    /// </summary>
    public string HotkeyString { get; }

    public RawHotkeyInputEventArgs(int keyCode, IReadOnlySet<int> pressedModifiers, string hotkeyString)
    {
        KeyCode = keyCode;
        PressedModifiers = pressedModifiers;
        HotkeyString = hotkeyString;
    }
}
