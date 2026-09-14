namespace CrossMacro.Core.Services.Scripting;

public static class ScriptConditionOperatorSyntax
{
    public static bool TryParse(string? token, out ScriptConditionOperator operation)
    {
        ScriptConditionOperator? parsed = token switch
        {
            "==" => ScriptConditionOperator.Equals,
            "!=" => ScriptConditionOperator.NotEquals,
            ">" => ScriptConditionOperator.GreaterThan,
            ">=" => ScriptConditionOperator.GreaterThanOrEqual,
            "<" => ScriptConditionOperator.LessThan,
            "<=" => ScriptConditionOperator.LessThanOrEqual,
            _ => null,
        };
        operation = parsed ?? ScriptConditionOperator.Equals;
        return parsed.HasValue;
    }

    public static string Format(ScriptConditionOperator operation) => operation switch
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
