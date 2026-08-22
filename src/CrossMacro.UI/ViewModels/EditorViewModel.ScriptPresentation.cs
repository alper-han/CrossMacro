
namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    public IReadOnlyList<string> AvailableVariableNames { get; private set; } = [];
    public bool HasAvailableVariableNames => AvailableVariableNames.Count > 0;
    public IEnumerable<ScriptConditionOperator> ScriptConditionOperators => GetConditionOperatorsForSelectedAction();
    public string ConditionRightOperandHint => (SelectedAction?.ScriptLeftOperandType) is ScriptOperandType.Color
|| (SelectedAction?.ScriptRightOperandType) is ScriptOperandType.Color
        ? Localize("Editor_ConditionColorHint")
        : string.Empty;

    public string? SelectedSetVariableSuggestion
    {
        get => _selectedSetVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedSetVariableSuggestion, value, nameof(SelectedSetVariableSuggestion), suggestion =>
        {
            if ((SelectedAction?.Type) is EditorActionType.SetVariable)
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
            if (SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable)
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
&& SelectedAction.ScriptLeftOperandType is ScriptOperandType.VariableReference)
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
&& SelectedAction.ScriptRightOperandType is ScriptOperandType.VariableReference)
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
            if ((SelectedAction?.Type) is EditorActionType.ForBlockStart)
            {
                SelectedAction.ForVariableName = suggestion;
            }
        });
    }

    public string? SelectedClipboardVariableSuggestion
    {
        get => _selectedClipboardVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedClipboardVariableSuggestion, value, nameof(SelectedClipboardVariableSuggestion), suggestion =>
        {
            if ((SelectedAction?.Type) is EditorActionType.ClipboardGet or EditorActionType.CopySelectionToVariable)
            {
                SelectedAction.ScriptVariableName = suggestion;
            }
        });
    }

    public bool ShowSetVariablePicker => ShowSetVariableFields && HasAvailableVariableNames;
    public bool ShowClipboardVariablePicker => (ShowClipboardGetFields || ShowCopySelectionToVariableFields) && HasAvailableVariableNames;
    public bool ShowIncDecVariablePicker => ShowIncDecFields && HasAvailableVariableNames;
    public bool ShowConditionLeftVariablePicker =>
        ShowConditionFields
&& HasAvailableVariableNames
&& (SelectedAction?.ScriptLeftOperandType) is ScriptOperandType.VariableReference;
    public bool ShowConditionLeftOperandTextBox =>
        ShowConditionFields
        && ((SelectedAction?.ScriptLeftOperandType) is not ScriptOperandType.VariableReference || !ShowConditionLeftVariablePicker);
    public bool ShowConditionLeftColorPicker =>
        ShowConditionFields
&& !IsCapturingConditionLeftColor
&& (_screenPixelReader?.IsSupported) is true
&& (SelectedAction?.ScriptLeftOperandType) is ScriptOperandType.Color;
    public bool ShowConditionRightVariablePicker =>
        ShowConditionFields
&& HasAvailableVariableNames
&& (SelectedAction?.ScriptRightOperandType) is ScriptOperandType.VariableReference;
    public bool ShowConditionRightOperandTextBox =>
        ShowConditionFields
        && ((SelectedAction?.ScriptRightOperandType) is not ScriptOperandType.VariableReference || !ShowConditionRightVariablePicker);
    public bool ShowConditionRightColorPicker =>
        ShowConditionFields
&& !IsCapturingConditionRightColor
&& (_screenPixelReader?.IsSupported) is true
&& (SelectedAction?.ScriptRightOperandType) is ScriptOperandType.Color;
    public bool ShowForVariablePicker => ShowForFields && HasAvailableVariableNames;

    private string? GetSelectedConditionVariableSuggestion(
        string? fallback,
        ScriptOperandType? operandType,
        string? operand)
    {
        if (operandType is ScriptOperandType.VariableReference
&& !string.IsNullOrWhiteSpace(operand)
&& AvailableVariableNames.Contains(operand, StringComparer.Ordinal))
        {
            return operand;
        }

        return fallback;
    }
}
