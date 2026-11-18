
namespace CrossMacro.TestInfrastructure;

public static class ConditionalSkipMessage
{
    public static string For(string requiredEnvironment) => $"Requires {requiredEnvironment}.";
}
