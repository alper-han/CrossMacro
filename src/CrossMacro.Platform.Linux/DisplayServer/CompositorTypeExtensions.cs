namespace CrossMacro.Platform.Linux.DisplayServer;

public static class CompositorTypeExtensions
{
    /// <summary>
    /// The session kind published in screen-reading diagnostics (<c>doctor --json</c> <c>sessionKind</c>).
    /// These strings are an external contract and do not follow the enum member names.
    /// </summary>
    public static string ToSessionKind(this CompositorType compositor) => compositor switch
    {
        CompositorType.Unknown => "Unknown",
        CompositorType.X11 => "X11",
        CompositorType.Hyprland => "HYPRLAND",
        CompositorType.Wayfire => "WAYFIRE",
        CompositorType.Niri => "NIRI",
        CompositorType.Cosmic => "COSMIC",
        CompositorType.Kde => "KDE",
        CompositorType.Gnome => "GNOME",
        CompositorType.Sway => "SWAY",
        CompositorType.Other => "Other",
        _ => throw new ArgumentOutOfRangeException(nameof(compositor), compositor, "Unknown compositor type."),
    };
}
