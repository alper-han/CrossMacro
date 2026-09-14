namespace CrossMacro.Core.Services.Scripting;

/// <summary>Lexical rules shared by runtime expressions and editor projections.</summary>
public static class ScriptIdentifierSyntax
{
    public static bool IsValidVariableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var token = value.Trim();
        var name = token.StartsWith('$') ? token[1..] : token;
        if (name.Length is 0 || name.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!IsVariableNameStart(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!IsVariableNamePart(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsVariableNameStart(char ch) => ch == '_' || char.IsLetter(ch);
    public static bool IsVariableNamePart(char ch) => ch == '_' || char.IsLetterOrDigit(ch);
}
