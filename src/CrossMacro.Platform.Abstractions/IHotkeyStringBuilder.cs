namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Builds human-readable hotkey strings from key codes and modifiers.
/// </summary>
public interface IHotkeyStringBuilder
{
    /// <summary>
    /// Builds a hotkey string from a key code and current modifiers.
    /// </summary>
    /// <param name="keyCode">The main key code</param>
    /// <param name="modifiers">The set of pressed modifier key codes</param>
    /// <returns>A formatted hotkey string (e.g., "Ctrl+Shift+P")</returns>
    string Build(int keyCode, IReadOnlySet<int> modifiers);

    /// <summary>
    /// Builds a hotkey string for a mouse button.
    /// </summary>
    /// <param name="buttonName">The mouse button name</param>
    /// <param name="modifiers">The set of pressed modifier key codes</param>
    /// <returns>A formatted hotkey string (e.g., "Ctrl+Mouse Side")</returns>
    string BuildForMouse(string buttonName, IReadOnlySet<int> modifiers);
}
