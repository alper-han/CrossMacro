namespace CrossMacro.Platform.Linux.Services;

/// <summary>The single place that turns session environment variables into a session kind and desktop identity.</summary>
internal static class LinuxDisplaySessionClassifier
{
    /// <summary>An explicit <c>XDG_SESSION_TYPE</c> wins; otherwise the display variables decide, Wayland first.</summary>
    internal static LinuxSessionType Classify(LinuxEnvironmentSnapshot environment)
    {
        if (string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return LinuxSessionType.Wayland;
        }

        if (string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return LinuxSessionType.X11;
        }

        if (!string.IsNullOrWhiteSpace(environment.WaylandDisplay))
        {
            return LinuxSessionType.Wayland;
        }

        return string.IsNullOrWhiteSpace(environment.Display) ? LinuxSessionType.Unknown : LinuxSessionType.X11;
    }

    internal static bool IsWayland(LinuxEnvironmentSnapshot environment) =>
        Classify(environment) is LinuxSessionType.Wayland;

    /// <summary>Matches one colon-separated component of <c>XDG_CURRENT_DESKTOP</c>, e.g. <c>ubuntu:GNOME</c>.</summary>
    internal static bool HasDesktop(LinuxEnvironmentSnapshot environment, string desktop)
    {
        var current = environment.CurrentDesktop.AsSpan();
        foreach (var component in current.Split(':'))
        {
            if (current[component].Trim().Equals(desktop, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsKde(LinuxEnvironmentSnapshot environment) =>
        HasDesktop(environment, "KDE") || HasDesktop(environment, "PLASMA");

    internal static bool IsGnome(LinuxEnvironmentSnapshot environment) => HasDesktop(environment, "GNOME");

    internal static bool GdmSessionContains(LinuxEnvironmentSnapshot environment, string name) =>
        environment.GdmSession?.Contains(name, StringComparison.OrdinalIgnoreCase) is true;
}
