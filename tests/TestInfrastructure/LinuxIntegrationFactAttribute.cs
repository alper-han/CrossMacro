
namespace CrossMacro.TestInfrastructure;

internal sealed class LinuxIntegrationFactAttribute : FactAttribute
{
    public LinuxIntegrationFactAttribute()
    {
        if (!(OperatingSystem.IsLinux() &&
              string.Equals(
                  Environment.GetEnvironmentVariable("CROSSMACRO_DAEMON_INTEGRATION_TESTS"),
                  "1",
                  StringComparison.Ordinal)))
        {
            Skip = ConditionalSkipMessage.For("Linux + CROSSMACRO_DAEMON_INTEGRATION_TESTS=1");
        }
    }
}
