
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class RunScriptRuntimeText
{
    /// <summary>
    /// Substitutes variable references for a service executor. An unknown variable is reported under
    /// <paramref name="errorPrefix"/>; a malformed reference is not.
    /// </summary>
    public static string ResolveVariables(string input, IDictionary<string, string> variables, string errorPrefix = "")
    {
        if (RunScriptVariables.TryResolveText(variables, input, RunScriptDialect.Execution, out var resolved, out var error))
        {
            return resolved;
        }

        throw new InvalidOperationException(error.IsUnknownVariable ? errorPrefix + error.Message : error.Message);
    }

    public static string NormalizeAndValidateVariableName(string variableName)
    {
        var normalized = EditorActionScriptTokens.NormalizeVariableToken(variableName);
        EnsureValidVariableName(normalized);
        return normalized;
    }

    public static void EnsureValidVariableName(string variableName)
    {
        if (!ScriptIdentifierSyntax.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException(ScriptMessages.InvalidVariableName(variableName));
        }
    }
}
