namespace CrossMacro.Core.Models;

/// <summary>
/// Operand source for if/while conditions.
/// </summary>
public enum ScriptOperandType
{
    VariableReference = 0,
    Number = 1,
    Text = 2,
    Boolean = 3,
    Color = 4,
}
