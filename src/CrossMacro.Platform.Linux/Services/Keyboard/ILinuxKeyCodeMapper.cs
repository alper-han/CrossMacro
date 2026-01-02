namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Maps Linux evdev keycodes to human-readable key names and vice versa.
/// Provides static mappings for modifiers, function keys, numpad, and XKB fallback.
/// </summary>
public interface ILinuxKeyCodeMapper
{
    /// <summary>
    /// Gets the display name for a given evdev keycode (e.g., 30 → "A", 57 → "Space").
    /// </summary>
    string GetKeyName(int keyCode);

    /// <summary>
    /// Gets the evdev keycode for a given key name (e.g., "Space" → 57).
    /// </summary>
    /// <returns>Keycode or -1 if not found</returns>
    int GetKeyCode(string keyName);

    /// <summary>
    /// Checks if the given keycode is a modifier key (Ctrl, Shift, Alt, Super).
    /// </summary>
    bool IsModifier(int keyCode);
}
