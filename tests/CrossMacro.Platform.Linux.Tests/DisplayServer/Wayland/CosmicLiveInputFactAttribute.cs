namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

internal sealed class CosmicLiveInputFactAttribute : FactAttribute
{
    private const string EnvironmentVariableName = "CROSSMACRO_LIVE_COSMIC_INPUT_TESTS";

    public CosmicLiveInputFactAttribute()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!OperatingSystem.IsLinux() ||
            !string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableName), "1", StringComparison.Ordinal) ||
            desktop?.Contains("COSMIC", StringComparison.OrdinalIgnoreCase) is not true)
        {
            Skip = $"Requires a COSMIC session + {EnvironmentVariableName}=1.";
        }
    }
}
