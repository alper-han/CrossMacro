
namespace CrossMacro.UI.ViewModels.Editor;

public partial class EditorViewModel
{
    private void RefreshAvailableVariableNames()
    {
        var next = EditorVariableCatalog.GetNames(Actions);
        var nextColor = EditorVariableCatalog.GetColorNames(Actions, SelectedAction);
        var variableNamesChanged = !AvailableVariableNames.SequenceEqual(next, StringComparer.Ordinal);
        var colorVariableNamesChanged = !AvailableColorVariableNames.SequenceEqual(nextColor, StringComparer.Ordinal);

        if (!variableNamesChanged && !colorVariableNamesChanged)
        {
            OnPropertyChanged(nameof(CanInsertElseBlock));
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(ShowSetVariablePicker));
            OnPropertyChanged(nameof(ShowIncDecVariablePicker));
            OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
            OnPropertyChanged(nameof(ShowConditionLeftOperandTextBox));
            OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
            OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
            OnPropertyChanged(nameof(ShowConditionRightOperandTextBox));
            OnPropertyChanged(nameof(ShowConditionRightColorPicker));
            OnPropertyChanged(nameof(ShowForVariablePicker));
            ClearVariableSuggestionSelections();
            return;
        }

        if (variableNamesChanged)
        {
            AvailableVariableNames = next;
            OnPropertyChanged(nameof(AvailableVariableNames));
            OnPropertyChanged(nameof(HasAvailableVariableNames));
        }

        if (colorVariableNamesChanged)
        {
            AvailableColorVariableNames = nextColor;
            OnPropertyChanged(nameof(AvailableColorVariableNames));
            OnPropertyChanged(nameof(HasAvailableColorVariableNames));
        }

        OnPropertyChanged(nameof(CanInsertElseBlock));
        OnPropertyChanged(nameof(CanRemoveBlock));
        OnPropertyChanged(nameof(ShowSetVariablePicker));
        OnPropertyChanged(nameof(ShowIncDecVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
        OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
        OnPropertyChanged(nameof(ShowConditionRightOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionRightColorPicker));
        OnPropertyChanged(nameof(ShowForVariablePicker));
        NotifyScriptArithmeticPresentationChanged();
        NotifyScreenReadingComputedPropertiesChanged();
        ClearVariableSuggestionSelections();
    }

    private void ClearVariableSuggestionSelections()
    {
        SetSuggestionValue(ref _selectedSetVariableSuggestion, nameof(SelectedSetVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedIncDecVariableSuggestion, nameof(SelectedIncDecVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionLeftVariableSuggestion, nameof(SelectedConditionLeftVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionRightVariableSuggestion, nameof(SelectedConditionRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedForVariableSuggestion, nameof(SelectedForVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedClipboardVariableSuggestion, nameof(SelectedClipboardVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedScreenTargetColorVariableSuggestion, nameof(SelectedScreenTargetColorVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedRepeatCountExprRightVariableSuggestion, nameof(SelectedRepeatCountExprRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedForStartExprRightVariableSuggestion, nameof(SelectedForStartExprRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedForEndExprRightVariableSuggestion, nameof(SelectedForEndExprRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedForStepExprRightVariableSuggestion, nameof(SelectedForStepExprRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionLeftExprRightVariableSuggestion, nameof(SelectedConditionLeftExprRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionRightExprRightVariableSuggestion, nameof(SelectedConditionRightExprRightVariableSuggestion), value: null);
    }

    private void SetSuggestionValue(ref string? targetField, string propertyName, string? value)
    {
        if (string.Equals(targetField, value, StringComparison.Ordinal))
        {
            return;
        }

        targetField = value;
        OnPropertyChanged(propertyName);
    }

    private void ApplyVariableSuggestion(
        ref string? field,
        string? value,
        string propertyName,
        Action<string> applyAction)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);

        if (_isApplyingVariableSuggestion || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _isApplyingVariableSuggestion = true;
        try
        {
            applyAction(value);
            field = null;
            OnPropertyChanged(propertyName);
        }
        finally
        {
            _isApplyingVariableSuggestion = false;
        }
    }

}
