namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Parses hotkey strings into structured mappings.
/// </summary>
public interface IHotkeyParser
{
    /// <summary>
    /// Parses a hotkey string into a mapping.
    /// </summary>
    /// <param name="hotkeyString">The hotkey string (e.g., "Ctrl+Shift+P")</param>
    /// <returns>The parsed hotkey mapping</returns>
    HotkeyMapping Parse(string hotkeyString);
}
