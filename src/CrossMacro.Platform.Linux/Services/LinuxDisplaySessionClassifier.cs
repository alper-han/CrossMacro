namespace CrossMacro.Platform.Linux.Services;

internal static class LinuxDisplaySessionClassifier
{
    internal static bool IsWayland(LinuxEnvironmentSnapshot environment)
    {
        if (string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(environment.WaylandDisplay);
    }
}
