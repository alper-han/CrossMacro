
namespace CrossMacro.TestInfrastructure;

public sealed class LinuxIntegrationFactAttribute : ConditionalFactAttribute
{
    public LinuxIntegrationFactAttribute()
        : base(
            () => OperatingSystem.IsLinux() &&
                  string.Equals(
                      Environment.GetEnvironmentVariable("CROSSMACRO_DAEMON_INTEGRATION_TESTS"),
                      "1",
                      StringComparison.Ordinal),
            "Linux + CROSSMACRO_DAEMON_INTEGRATION_TESTS=1")
    {
    }
}
