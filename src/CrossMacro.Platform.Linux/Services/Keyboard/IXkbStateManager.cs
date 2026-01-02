namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Manages XKB context, keymap, and state for character-to-keycode translation.
/// Handles modifier state tracking and character input cache.
/// </summary>
public interface IXkbStateManager : IDisposable
{
    /// <summary>
    /// Whether XKB is successfully initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes XKB with the specified layout (e.g., "tr", "us").
    /// </summary>
    void Initialize(string? layout);

    /// <summary>
    /// Gets the UTF-8 string for a keycode using current XKB state.
    /// </summary>
    string? GetUtf8String(uint keycode);

    /// <summary>
    /// Gets the character produced by a keycode with the given modifier state.
    /// </summary>
    char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock);

    /// <summary>
    /// Finds the keycode and modifiers required to produce a specific character.
    /// </summary>
    (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c);
}
