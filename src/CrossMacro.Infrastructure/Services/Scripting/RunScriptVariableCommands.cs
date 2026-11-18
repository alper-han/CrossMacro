namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Syntax and arithmetic of the <c>set</c>, <c>inc</c>, <c>dec</c>, <c>mul</c> and <c>div</c> commands,
/// shared by the static expander and the runtime executor so both interpret them identically.
/// </summary>
internal static class RunScriptVariableCommands
{
    private const string InvalidSetSyntax = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";

    public static bool IsVariableStep(string step) =>
        step.StartsWith("set ", StringComparison.OrdinalIgnoreCase)
        || TryGetUpdateKind(step, out _);

    /// <summary>
    /// Returns true when <paramref name="step"/> is a <c>set</c> command; <paramref name="error"/> is then
    /// non-null when the command is malformed.
    /// </summary>
    public static bool TryParseAssignment(string step, out VariableAssignment assignment, out string? error)
    {
        assignment = default;
        error = null;

        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        string nameToken;
        string valueToken;
        var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
        if (equalIndex >= 0)
        {
            nameToken = payload[..equalIndex].Trim();
            valueToken = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is not 2)
            {
                error = InvalidSetSyntax;
                return true;
            }

            nameToken = parts[0];
            valueToken = parts[1];
        }

        if (!TryNormalizeName(nameToken, out var name, out error))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(valueToken))
        {
            error = $"Variable '{name}' value cannot be empty.";
            return true;
        }

        assignment = new VariableAssignment(name, valueToken);
        return true;
    }

    /// <summary>
    /// Returns true when <paramref name="step"/> is an <c>inc</c>, <c>dec</c>, <c>mul</c> or <c>div</c> command;
    /// <paramref name="error"/> is then non-null when the command is malformed.
    /// </summary>
    public static bool TryParseUpdate(string step, out VariableUpdate update, out string? error)
    {
        update = default;
        error = null;

        if (!TryGetUpdateKind(step, out var kind))
        {
            return false;
        }

        var parts = step[4..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2)
        {
            var command = CommandName(kind);
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        if (!TryNormalizeName(parts[0], out var name, out error))
        {
            return true;
        }

        update = new VariableUpdate(kind, name, parts.Length is 2 ? parts[1] : "1");
        return true;
    }

    /// <summary>Accepts an optional leading <c>$</c> and returns the bare, validated variable name.</summary>
    public static bool TryNormalizeName(string token, out string name, out string? error)
    {
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Variable name cannot be empty.";
            return false;
        }

        var normalized = EditorActionScriptTokens.NormalizeVariableToken(token);
        if (normalized.StartsWith('$') || !ScriptIdentifierSyntax.IsValidVariableName(normalized))
        {
            error = $"Invalid variable name '{token}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return false;
        }

        name = normalized;
        error = null;
        return true;
    }

    public static bool TryReadInteger(VariableUpdate update, string? currentValue, out int value, out string? error)
    {
        if (currentValue is not null
            && int.TryParse(currentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        value = 0;
        error = $"variable '{update.Name}' must exist and be an integer for {update.Family}.";
        return false;
    }

    public static bool TryApply(VariableUpdate update, int current, int amount, out int result, out string? error)
    {
        result = 0;
        long updated;
        switch (update.Kind)
        {
            case VariableUpdateKind.Increment:
                updated = (long)current + amount;
                break;
            case VariableUpdateKind.Decrement:
                updated = (long)current - amount;
                break;
            case VariableUpdateKind.Multiply:
                updated = (long)current * amount;
                break;
            default:
                if (amount is 0)
                {
                    error = "Division by zero is not allowed in mul/div.";
                    return false;
                }

                updated = (long)current / amount;
                break;
        }

        if (updated is < int.MinValue or > int.MaxValue)
        {
            error = $"Result is out of range for {update.Family}.";
            return false;
        }

        result = (int)updated;
        error = null;
        return true;
    }

    /// <summary>
    /// Computes the stored value of a <c>set</c> command from its variable-resolved value token: numeric
    /// expressions are evaluated, anything else is stored verbatim.
    /// </summary>
    public static bool TryEvaluateAssignedValue(
        string resolvedValue,
        IReadOnlyDictionary<string, string> variables,
        out string stored,
        out string? error)
    {
        // A surviving '$' is a '$$' escape; keep the raw-string fallback.
        if (!resolvedValue.Contains('$', StringComparison.Ordinal)
            && ScriptNumericExpression.TryParse(resolvedValue, out var expression)
            && expression is not null)
        {
            if (!ScriptNumericExpression.Evaluate(expression, variables, out var numericValue, out error))
            {
                stored = string.Empty;
                return false;
            }

            stored = numericValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        stored = resolvedValue;
        error = null;
        return true;
    }

    private static bool TryGetUpdateKind(string step, out VariableUpdateKind kind)
    {
        if (step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase))
        {
            kind = VariableUpdateKind.Increment;
        }
        else if (step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            kind = VariableUpdateKind.Decrement;
        }
        else if (step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase))
        {
            kind = VariableUpdateKind.Multiply;
        }
        else if (step.StartsWith("div ", StringComparison.OrdinalIgnoreCase))
        {
            kind = VariableUpdateKind.Divide;
        }
        else
        {
            kind = default;
            return false;
        }

        return true;
    }

    private static string CommandName(VariableUpdateKind kind) => kind switch
    {
        VariableUpdateKind.Increment => "inc",
        VariableUpdateKind.Decrement => "dec",
        VariableUpdateKind.Multiply => "mul",
        VariableUpdateKind.Divide => "div",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown variable update kind."),
    };
}
