using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Views.Tabs;

public static class EditorScriptDisplayConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public static string FormatOperandType(ScriptOperandType operandType)
    {
        return operandType switch
        {
            ScriptOperandType.VariableReference => Localize("Editor_ScriptOperand_VariableReference", "Variable"),
            ScriptOperandType.Number => Localize("Editor_ScriptOperand_Number", "Number"),
            ScriptOperandType.Text => Localize("Editor_ScriptOperand_Text", "Text"),
            ScriptOperandType.Boolean => Localize("Editor_ScriptOperand_Boolean", "True / False"),
            ScriptOperandType.Color => Localize("Editor_ScriptOperand_Color", "Color (RRGGBB)"),
            _ => operandType.ToString(),
        };
    }

    public static string FormatConditionOperator(ScriptConditionOperator conditionOperator)
    {
        return conditionOperator switch
        {
            ScriptConditionOperator.Equals => Localize("Editor_ScriptConditionOperator_Equals", "Equals (=)"),
            ScriptConditionOperator.NotEquals => Localize("Editor_ScriptConditionOperator_NotEquals", "Not equals (!=)"),
            ScriptConditionOperator.GreaterThan => Localize("Editor_ScriptConditionOperator_GreaterThan", "Greater than (>)"),
            ScriptConditionOperator.GreaterThanOrEqual => Localize("Editor_ScriptConditionOperator_GreaterThanOrEqual", "Greater than or equal (>=)"),
            ScriptConditionOperator.LessThan => Localize("Editor_ScriptConditionOperator_LessThan", "Less than (<)"),
            ScriptConditionOperator.LessThanOrEqual => Localize("Editor_ScriptConditionOperator_LessThanOrEqual", "Less than or equal (<=)"),
            _ => conditionOperator.ToString(),
        };
    }

    private static string Localize(string key, string fallback)
    {
        var localized = _localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}
