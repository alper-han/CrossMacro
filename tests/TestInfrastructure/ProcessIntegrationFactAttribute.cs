namespace CrossMacro.TestInfrastructure;

internal sealed class ProcessIntegrationFactAttribute : FactAttribute
{
    private const string IntegrationEnvironmentVariable = "CROSSMACRO_PROCESS_INTEGRATION_TESTS";

    public ProcessIntegrationFactAttribute()
    {
        if (!((OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) &&
              string.Equals(
                  Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable),
                  "1",
                  StringComparison.Ordinal)))
        {
            Skip = ConditionalSkipMessage.For("Linux/Windows/macOS + CROSSMACRO_PROCESS_INTEGRATION_TESTS=1");
        }
    }
}
