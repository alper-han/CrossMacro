namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Abstracts keyboard layout detection across different Linux desktop environments.
/// Supports: Hyprland, KDE Plasma, GNOME, IBus, X11, and localectl fallback.
/// </summary>
public interface ILinuxLayoutDetector
{
    /// <summary>
    /// Detects the active keyboard layout (e.g., "tr", "us", "de").
    /// Priority: DE-specific > IBus > X11 > localectl
    /// </summary>
    /// <returns>Layout code or null if detection fails</returns>
    string? DetectLayout();
}
