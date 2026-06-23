using System;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class WaylandLiveSmokeFactAttribute : FactAttribute
{
    private const string EnvironmentVariableName = "CROSSMACRO_LIVE_WAYLAND_SCREEN_READER_TESTS";

    public WaylandLiveSmokeFactAttribute()
        : this(OperatingSystem.IsLinux() && string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            "1",
            StringComparison.Ordinal))
    {
    }

    private WaylandLiveSmokeFactAttribute(bool enabled)
    {
        if (!enabled)
        {
            Skip = $"Requires Linux + {EnvironmentVariableName}=1.";
        }
    }
}
