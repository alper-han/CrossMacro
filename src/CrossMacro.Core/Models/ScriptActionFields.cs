namespace CrossMacro.Core.Models;

/// <summary>
/// Value source for set-variable actions.
/// </summary>
public enum ScriptValueType
{
    Number = 0,
    Text = 1,
    Boolean = 2,
    VariableReference = 3
}

/// <summary>
/// Numeric token source for repeat/inc/dec/for actions.
/// </summary>
public enum ScriptNumericSourceType
{
    Number = 0,
    VariableReference = 1
}

/// <summary>
/// Operand source for if/while conditions.
/// </summary>
public enum ScriptOperandType
{
    VariableReference = 0,
    Number = 1,
    Text = 2,
    Boolean = 3
}

/// <summary>
/// Supported operators for if/while conditions.
/// </summary>
public enum ScriptConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5
}
