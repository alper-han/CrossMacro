
namespace CrossMacro.TestInfrastructure;

internal static class ConditionalSkipMessage
{
    internal static string For(string requiredEnvironment) => $"Requires {requiredEnvironment}.";
}
