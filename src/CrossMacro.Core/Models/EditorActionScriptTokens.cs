
namespace CrossMacro.Core.Models;

/// <summary>
/// Shared token parsing, validation, and formatting helpers for script-backed editor actions.
/// </summary>
public static class EditorActionScriptTokens
{
    public static bool IsValidVariableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var name = NormalizeVariableToken(value);
        if (name.Length is 0)
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

    public static string NormalizeVariableToken(string value)
    {
        var token = value?.Trim() ?? string.Empty;
        return token.StartsWith('$') ? token[1..] : token;
    }

    public static bool ValidateNumericToken(ScriptNumericSourceType sourceType, string token)
    {
        if (sourceType is ScriptNumericSourceType.VariableReference)
        {
            return IsValidVariableName(token);
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public static bool ValidateOperandToken(ScriptOperandType operandType, string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return operandType switch
        {
            ScriptOperandType.VariableReference => IsValidVariableName(token),
            ScriptOperandType.Number => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScriptOperandType.Boolean => bool.TryParse(token, out _),
            ScriptOperandType.Color => TryFormatRgbHexColor(token, out _),
            ScriptOperandType.Text => !string.IsNullOrWhiteSpace(token),
            _ => false,
        };
    }

    public static string FormatNumericToken(ScriptNumericSourceType sourceType, string value, string defaultValue = "0")
    {
        var token = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        return sourceType is ScriptNumericSourceType.VariableReference
            ? $"${NormalizeVariableToken(token)}"
            : token;
    }

    public static string FormatOperandToken(ScriptOperandType operandType, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var token = value.Trim();
        return operandType switch
        {
            ScriptOperandType.VariableReference => $"${NormalizeVariableToken(token)}",
            ScriptOperandType.Number => token,
            ScriptOperandType.Text => EscapeLiteralDollar(token),
            ScriptOperandType.Boolean => token,
            ScriptOperandType.Color => TryFormatRgbHexColor(token, out var color) ? color : token,
            _ => token,
        };
    }

    public static string FormatSetValueToken(ScriptValueType valueType, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (valueType)
        {
            case ScriptValueType.VariableReference:
                return $"${NormalizeVariableToken(value)}";
            case ScriptValueType.Number:
                return value.Trim();
            case ScriptValueType.Text:
                return EscapeLiteralDollar(value.Trim());
            case ScriptValueType.Boolean:
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue ? "true" : "false";
                }

                return value.Trim();
            default:
                return value.Trim();
        }
    }

    public static string ToOperatorToken(ScriptConditionOperator op)
    {
        return op switch
        {
            ScriptConditionOperator.Equals => "==",
            ScriptConditionOperator.NotEquals => "!=",
            ScriptConditionOperator.GreaterThan => ">",
            ScriptConditionOperator.GreaterThanOrEqual => ">=",
            ScriptConditionOperator.LessThan => "<",
            ScriptConditionOperator.LessThanOrEqual => "<=",
            _ => "==",
        };
    }

    public static string EscapeLiteralDollar(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Replace("$", "$$", StringComparison.Ordinal);
    }

    public static string UnescapeLiteralDollar(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Replace("$$", "$", StringComparison.Ordinal);
    }

    public static bool IsVariableNameStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    public static bool IsVariableNamePart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }

    private static bool TryFormatRgbHexColor(string value, out string color)
    {
        var token = value.Trim();
        if (token.Length is not 6)
        {
            color = string.Empty;
            return false;
        }

        if (!token.All(Uri.IsHexDigit))
        {
            color = string.Empty;
            return false;
        }

        color = token.ToUpperInvariant();
        return true;
    }
}
