
namespace CrossMacro.UI.Converters;

public static class EditorScriptDisplayConverters
{
    public static string FormatOperandType(ScriptOperandType operandType, ILocalizationService? localizationService = null)
    {
        return operandType switch
        {
            ScriptOperandType.VariableReference => Localize("Editor_ScriptOperand_VariableReference", "Variable", localizationService),
            ScriptOperandType.Number => Localize("Editor_ScriptOperand_Number", "Number", localizationService),
            ScriptOperandType.Text => Localize("Editor_ScriptOperand_Text", "Text", localizationService),
            ScriptOperandType.Boolean => Localize("Editor_ScriptOperand_Boolean", "True / False", localizationService),
            ScriptOperandType.Color => Localize("Editor_ScriptOperand_Color", "Color (RRGGBB)", localizationService),
            _ => operandType.ToString(),
        };
    }

    public static string FormatConditionOperator(ScriptConditionOperator conditionOperator, ILocalizationService? localizationService = null)
    {
        return conditionOperator switch
        {
            ScriptConditionOperator.Equals => Localize("Editor_ScriptConditionOperator_Equals", "Equals (=)", localizationService),
            ScriptConditionOperator.NotEquals => Localize("Editor_ScriptConditionOperator_NotEquals", "Not equals (!=)", localizationService),
            ScriptConditionOperator.GreaterThan => Localize("Editor_ScriptConditionOperator_GreaterThan", "Greater than (>)", localizationService),
            ScriptConditionOperator.GreaterThanOrEqual => Localize("Editor_ScriptConditionOperator_GreaterThanOrEqual", "Greater than or equal (>=)", localizationService),
            ScriptConditionOperator.LessThan => Localize("Editor_ScriptConditionOperator_LessThan", "Less than (<)", localizationService),
            ScriptConditionOperator.LessThanOrEqual => Localize("Editor_ScriptConditionOperator_LessThanOrEqual", "Less than or equal (<=)", localizationService),
            _ => conditionOperator.ToString(),
        };
    }

    /// <summary>
    /// Maps an operation to its culture-invariant glyph (+ − × ÷); intentionally not localized.
    /// </summary>
    public static string FormatArithmeticOperation(ScriptArithmeticOperation operation)
    {
        return operation switch
        {
            ScriptArithmeticOperation.Add => "+",
            ScriptArithmeticOperation.Subtract => "−",
            ScriptArithmeticOperation.Multiply => "×",
            ScriptArithmeticOperation.Divide => "÷",
            ScriptArithmeticOperation.Modulo => "%",
            _ => operation.ToString(),
        };
    }

    private static string Localize(string key, string fallback, ILocalizationService? localizationService)
    {
        var localized = localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}
