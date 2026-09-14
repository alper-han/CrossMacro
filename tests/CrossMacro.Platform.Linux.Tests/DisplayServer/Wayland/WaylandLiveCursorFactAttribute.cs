using System.Runtime.CompilerServices;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

internal sealed class WaylandLiveCursorFactAttribute : FactAttribute
{
    private const string EnvironmentVariableName = "CROSSMACRO_LIVE_WAYLAND_CURSOR_TESTS";

    public WaylandLiveCursorFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!IsEnabled(
                OperatingSystem.IsLinux(),
                Environment.GetEnvironmentVariable(EnvironmentVariableName),
                Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
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
