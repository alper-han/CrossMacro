namespace CrossMacro.Core.Models;

/// <summary>
/// Value source for set-variable actions.
/// </summary>
public enum ScriptValueType
{
    Number = 0,
    Text = 1,
    Boolean = 2,
    VariableReference = 3,
}
