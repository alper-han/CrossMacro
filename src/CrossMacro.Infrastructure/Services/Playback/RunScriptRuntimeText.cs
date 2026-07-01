using System;
using System.Collections.Generic;
using System.Text;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services.Playback;

internal static class RunScriptRuntimeText
{
    public static string ResolveVariables(string input, IDictionary<string, string> variables, string errorPrefix = "")
    {
        if (!input.Contains('$', StringComparison.Ordinal))
        {
            return input;
        }

        var output = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '$')
            {
                output.Append(input[i]);
                continue;
            }

            if (i + 1 < input.Length && input[i + 1] == '$')
            {
                output.Append('$');
                i++;
                continue;
            }

            var j = i + 1;
            while (j < input.Length && EditorActionScriptTokens.IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            EnsureValidVariableName(variableName);
            if (!variables.TryGetValue(variableName, out var value))
            {
                throw new InvalidOperationException($"{errorPrefix}Unknown variable '${variableName}'.");
            }

            output.Append(value);
            i = j - 1;
        }

        return output.ToString();
    }

    public static string NormalizeAndValidateVariableName(string variableName)
    {
        var normalized = EditorActionScriptTokens.NormalizeVariableToken(variableName);
        EnsureValidVariableName(normalized);
        return normalized;
    }

    public static void EnsureValidVariableName(string variableName)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException($"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*");
        }
    }

    public static string Unquote(string input)
    {
        if (input.Length >= 2
            && ((input[0] == '"' && input[^1] == '"')
                || (input[0] == '\'' && input[^1] == '\'')))
        {
            return input[1..^1];
        }

        return input;
    }
}
