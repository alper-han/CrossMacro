namespace CrossMacro.Core.Models;

/// <summary>
/// Stores global hotkey configurations
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// Hotkey for toggling recording (default: F8)
    /// Format: "F8", "J", "Super+J", "Ctrl+Shift+A"
    /// </summary>
    public string RecordingHotkey { get; set; } = "F8";

    /// <summary>
    /// Hotkey for toggling playback (default: F9)
    /// </summary>
    public string PlaybackHotkey { get; set; } = "F9";

    /// <summary>
    /// Hotkey for toggling pause (default: F10)
    /// </summary>
    public string PauseHotkey { get; set; } = "F10";
}
