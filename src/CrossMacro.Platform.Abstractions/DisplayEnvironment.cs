namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Platform-agnostic display environment types.
/// Used for UI decisions that depend on window manager behavior.
/// </summary>
public enum DisplayEnvironment
{
    /// <summary>
    /// Unknown or unsupported environment.
    /// </summary>
    Unknown,

    /// <summary>
    /// Microsoft Windows.
    /// </summary>
    Windows,

    /// <summary>
    /// Apple macOS.
    /// </summary>
    MacOS,

    /// <summary>
    /// Linux with X11 display server.
    /// </summary>
    LinuxX11,

    /// <summary>
    /// Linux with generic Wayland compositor.
    /// </summary>
    LinuxWayland,

    /// <summary>
    /// Linux with Hyprland compositor (tiling WM - handles its own decorations).
    /// </summary>
    LinuxHyprland,

    /// <summary>
    /// Linux with Wayfire compositor.
    /// </summary>
    LinuxWayfire,

    /// <summary>
    /// Linux with KDE Plasma desktop.
    /// </summary>
    LinuxKDE,

    /// <summary>
    /// Linux with GNOME desktop.
    /// </summary>
    LinuxGnome
}
