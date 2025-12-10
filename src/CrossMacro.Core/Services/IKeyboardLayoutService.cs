namespace CrossMacro.Core.Services;

/// <summary>
/// Service for handling keyboard layout specific operations (key names, character mapping via XKB)
/// </summary>
public interface IKeyboardLayoutService
{
    /// <summary>
    /// Gets the display name for a given key code based on the active layout (e.g. "Ö", "Enter")
    /// </summary>
    string GetKeyName(int keyCode);

    /// <summary>
    /// Gets the key code for a given key name based on the active layout
    /// </summary>
    int GetKeyCode(string keyName);

    /// <summary>
    /// Gets the character produced by a key code with given modifiers (e.g. 51 -> 'ö', 51+Shift -> 'Ö')
    /// </summary>
    char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock);

    /// <summary>
    /// Finds the key code and modifiers required to produce a specific character
    /// </summary>
    (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c);
}
