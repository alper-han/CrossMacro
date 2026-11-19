namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Detected display server / compositor types
/// </summary>
public enum CompositorType
{
    Unknown,
    X11,
    Hyprland,
    Wayfire,
    Niri,
    Cosmic,
    Kde,
    Gnome,
    Sway,
    Other,
}
