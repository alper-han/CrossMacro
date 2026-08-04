namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

internal sealed class WaylandLiveCursorFactAttribute : FactAttribute
{
    private const string EnvironmentVariableName = "CROSSMACRO_LIVE_WAYLAND_CURSOR_TESTS";

    public WaylandLiveCursorFactAttribute()
        : this(OperatingSystem.IsLinux() && string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            "1",
            StringComparison.Ordinal))
    {
    }

    private WaylandLiveCursorFactAttribute(bool enabled)
    {
        if (!enabled)
        {
            Skip = $"Requires Linux + {EnvironmentVariableName}=1.";
        }
    }
}
