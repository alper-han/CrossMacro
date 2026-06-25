using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    public IReadOnlyList<string> AvailableVariableNames => _availableVariableNames;
    public bool HasAvailableVariableNames => AvailableVariableNames.Count > 0;
    public IEnumerable<ScriptConditionOperator> ScriptConditionOperators => GetConditionOperatorsForSelectedAction();
    public string ConditionRightOperandHint => SelectedAction?.ScriptLeftOperandType == ScriptOperandType.Color
        || SelectedAction?.ScriptRightOperandType == ScriptOperandType.Color
        ? Localize("Editor_ConditionColorHint")
        : string.Empty;

    public string? SelectedSetVariableSuggestion
    {
        get => _selectedSetVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedSetVariableSuggestion, value, nameof(SelectedSetVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type == EditorActionType.SetVariable)
            {
                SelectedAction.ScriptVariableName = suggestion;
            }
        });
    }

    public string? SelectedIncDecVariableSuggestion
    {
        get => _selectedIncDecVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedIncDecVariableSuggestion, value, nameof(SelectedIncDecVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable)
            {
                SelectedAction.ScriptVariableName = suggestion;
            }
        });
    }

    public string? SelectedConditionLeftVariableSuggestion
    {
        get => GetSelectedConditionVariableSuggestion(
            _selectedConditionLeftVariableSuggestion,
            SelectedAction?.ScriptLeftOperandType,
            SelectedAction?.ScriptLeftOperand);
        set => ApplyVariableSuggestion(ref _selectedConditionLeftVariableSuggestion, value, nameof(SelectedConditionLeftVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && SelectedAction.ScriptLeftOperandType == ScriptOperandType.VariableReference)
            {
                SelectedAction.ScriptLeftOperand = suggestion;
            }
        });
    }

    public string? SelectedConditionRightVariableSuggestion
    {
        get => GetSelectedConditionVariableSuggestion(
            _selectedConditionRightVariableSuggestion,
            SelectedAction?.ScriptRightOperandType,
            SelectedAction?.ScriptRightOperand);
        set => ApplyVariableSuggestion(ref _selectedConditionRightVariableSuggestion, value, nameof(SelectedConditionRightVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && SelectedAction.ScriptRightOperandType == ScriptOperandType.VariableReference)
            {
                SelectedAction.ScriptRightOperand = suggestion;
            }
        });
    }

    public string? SelectedForVariableSuggestion
    {
        get => _selectedForVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedForVariableSuggestion, value, nameof(SelectedForVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type == EditorActionType.ForBlockStart)
            {
                SelectedAction.ForVariableName = suggestion;
            }
        });
    }

    public bool ShowSetVariablePicker => ShowSetVariableFields && HasAvailableVariableNames;
    public bool ShowIncDecVariablePicker => ShowIncDecFields && HasAvailableVariableNames;
    public bool ShowConditionLeftVariablePicker =>
        ShowConditionFields
        && HasAvailableVariableNames
        && SelectedAction?.ScriptLeftOperandType == ScriptOperandType.VariableReference;
    public bool ShowConditionLeftOperandTextBox =>
        ShowConditionFields
        && (SelectedAction?.ScriptLeftOperandType != ScriptOperandType.VariableReference || !ShowConditionLeftVariablePicker);
    public bool ShowConditionLeftColorPicker =>
        ShowConditionFields
        && !IsCapturing
        && _screenPixelReader?.IsSupported == true
        && SelectedAction?.ScriptLeftOperandType == ScriptOperandType.Color;
    public bool ShowConditionRightVariablePicker =>
        ShowConditionFields
        && HasAvailableVariableNames
        && SelectedAction?.ScriptRightOperandType == ScriptOperandType.VariableReference;
    public bool ShowConditionRightOperandTextBox =>
        ShowConditionFields
        && (SelectedAction?.ScriptRightOperandType != ScriptOperandType.VariableReference || !ShowConditionRightVariablePicker);
    public bool ShowConditionRightColorPicker =>
        ShowConditionFields
        && !IsCapturing
        && _screenPixelReader?.IsSupported == true
        && SelectedAction?.ScriptRightOperandType == ScriptOperandType.Color;
    public bool ShowForVariablePicker => ShowForFields && HasAvailableVariableNames;

    private string? GetSelectedConditionVariableSuggestion(
        string? fallback,
        ScriptOperandType? operandType,
        string? operand)
    {
        if (operandType == ScriptOperandType.VariableReference
            && !string.IsNullOrWhiteSpace(operand)
            && AvailableVariableNames.Contains(operand, StringComparer.Ordinal))
        {
            return operand;
        }

        return fallback;
    }
}
