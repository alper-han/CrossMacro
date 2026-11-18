using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

public sealed class ProcessIntegrationFactAttribute : FactAttribute
{
    private const string IntegrationEnvironmentVariable = "CROSSMACRO_PROCESS_INTEGRATION_TESTS";

    public ProcessIntegrationFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Skip = ConditionalSkipMessage.For("CROSSMACRO_PROCESS_INTEGRATION_TESTS=1");
        }
    }
}
