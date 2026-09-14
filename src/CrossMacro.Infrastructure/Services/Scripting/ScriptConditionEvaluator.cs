namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Condition value semantics shared by static expansion and runtime execution.</summary>
internal static class ScriptConditionEvaluator
{
    public readonly record struct Result(bool Success, bool Value, string ErrorMessage, string? InvalidVariableName = null)
    {
        public static Result Evaluated(bool value) => new(
            Success: true,
            Value: value,
            ErrorMessage: string.Empty);

        public static Result Invalid(string error, string? invalidVariableName = null) => new(
            Success: false,
            Value: false,
            ErrorMessage: error,
            InvalidVariableName: invalidVariableName);
    }

    public static Result Evaluate(string leftToken, string operatorToken, string rightToken, IReadOnlyDictionary<string, string> variables)
    {
        if (operatorToken is "==" or "!=")
        {
            var left = ResolveOperand(leftToken, variables);
            if (left.Error is not null)
            {
                return Result.Invalid(left.Error, left.InvalidVariableName);
            }
            var right = ResolveOperand(rightToken, variables);
            if (right.Error is not null)
            {
                return Result.Invalid(right.Error, right.InvalidVariableName);
            }
            var equal = ValuesEqual(left.Value!, right.Value!);
            return Result.Evaluated(operatorToken is "==" ? equal : !equal);
        }

        var numericLeft = ResolveNumericOperand(leftToken, variables);
        if (numericLeft.Error is not null)
        {
            return Result.Invalid(numericLeft.Error, numericLeft.InvalidVariableName);
        }
        var numericRight = ResolveNumericOperand(rightToken, variables);
        if (numericRight.Error is not null)
        {
            return Result.Invalid(numericRight.Error, numericRight.InvalidVariableName);
        }
        if (numericLeft.Value is not { } leftInt || numericRight.Value is not { } rightInt)
        {
            return Result.Invalid(
                $"Operator '{operatorToken}' requires numeric operands. Got '{numericLeft.Display ?? leftToken}' and '{numericRight.Display ?? rightToken}'.");
        }
        return operatorToken switch
        {
            ">" => Result.Evaluated(leftInt > rightInt),
            ">=" => Result.Evaluated(leftInt >= rightInt),
            "<" => Result.Evaluated(leftInt < rightInt),
            "<=" => Result.Evaluated(leftInt <= rightInt),
            _ => Result.Invalid($"Unsupported condition operator '{operatorToken}'."),
        };
    }

    private static (int? Value, string? Display, string? Error, string? InvalidVariableName) ResolveNumericOperand(string token, IReadOnlyDictionary<string, string> variables)
    {
        if (ScriptNumericExpression.TryParse(token, out var expression) && expression is { Op: not null })
        {
            var evaluated = ScriptNumericExpression.Evaluate(token, variables, "condition operand");
            return evaluated.Status is ScriptNumericExpressionStatus.Evaluated
                ? (evaluated.Value, null, null, null)
                : (null, null, evaluated.Error, null);
        }
        var resolved = ResolveOperand(token, variables);
        if (resolved.Error is not null)
        {
            return (null, null, resolved.Error, resolved.InvalidVariableName);
        }
        return ScriptNumericExpression.TryEvaluate(resolved.Value!, variables, out var value, out _)
            ? (value, resolved.Value, null, null)
            : (null, resolved.Value, null, null);
    }

    private static (string? Value, string? Error, string? InvalidVariableName) ResolveOperand(string token, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, "Condition token cannot be empty.", null);
        }
        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            return (Unquote(EditorActionScriptTokens.UnescapeLiteralDollar(token)), null, null);
        }
        if (token.StartsWith('$'))
        {
            var name = token[1..];
            if (!EditorActionScriptTokens.IsValidVariableName(name))
            {
                return (null, $"Invalid variable reference '{token}'.", name);
            }
            return variables.TryGetValue(name, out var value)
                ? (Unquote(value), null, null)
                : (null, $"Unknown variable '${name}'.", null);
        }
        return (EditorActionScriptTokens.UnescapeLiteralDollar(Unquote(token)), null, null);
    }

    private static bool ValuesEqual(string left, string right)
    {
        if (ScreenPixelColor.TryParse(left, out var leftColor) && ScreenPixelColor.TryParse(right, out var rightColor))
        {
            return leftColor.Equals(rightColor);
        }
        if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftInt)
            && int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightInt))
        {
            return leftInt == rightInt;
        }
        if (bool.TryParse(left, out var leftBool) && bool.TryParse(right, out var rightBool))
        {
            return leftBool == rightBool;
        }
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static string Unquote(string input) => input.Length >= 2
        && ((input[0] is '"' && input[^1] is '"') || (input[0] is '\'' && input[^1] is '\''))
        ? input[1..^1] : input;
}
