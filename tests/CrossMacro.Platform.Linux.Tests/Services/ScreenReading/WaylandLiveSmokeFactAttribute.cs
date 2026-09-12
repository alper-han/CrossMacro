
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

internal sealed class WaylandLiveSmokeFactAttribute : FactAttribute
{
    private const string EnvironmentVariableName = "CROSSMACRO_LIVE_WAYLAND_SCREEN_READER_TESTS";

    public WaylandLiveSmokeFactAttribute()
        : this(IsEnabled(
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
    {
    }

    private WaylandLiveSmokeFactAttribute(bool enabled)
    {
        if (!enabled)
        {
            Skip = $"Requires Linux + {EnvironmentVariableName}=1.";
        }
    }

    internal static bool IsEnabled(
        bool isLinux,
        string? optInValue,
        string? sessionType,
        string? waylandDisplay)
    {
        if (!isLinux || !string.Equals(optInValue, "1", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(waylandDisplay);
    }
}
