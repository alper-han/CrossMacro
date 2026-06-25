using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private IReadOnlyList<string> BuildAvailableVariableNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Actions.Count; index++)
        {
            var action = Actions[index];
            switch (action.Type)
            {
                case EditorActionType.SetVariable:
                    AddIfValidVariableName(names, action.ScriptVariableName);
                    if (action.PreferLegacyScriptText)
                    {
                        TryAddLegacySetVariableName(names, action.Text);
                    }
                    break;
                case EditorActionType.ForBlockStart:
                    AddIfValidVariableName(names, action.ForVariableName);
                    break;
            }

            if (action.TryGetScreenReadingPayload(out var screenReadingPayload))
            {
                foreach (var variableName in screenReadingPayload.GetOutputVariableNames())
                {
                    AddIfValidVariableName(names, variableName);
                }
            }
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<string> BuildAvailableColorVariableNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var selectedIndex = SelectedAction is null ? -1 : Actions.IndexOf(SelectedAction);
        var actionCount = selectedIndex >= 0 ? selectedIndex : Actions.Count;

        for (var index = 0; index < actionCount; index++)
        {
            var action = Actions[index];
            if (!action.TryGetScreenReadingPayload(out var screenReadingPayload))
            {
                continue;
            }

            foreach (var variableName in screenReadingPayload.GetOutputVariableNames())
            {
                if (screenReadingPayload.GetOutputVariableRole(variableName) == EditorActionScreenReadingVariableRole.Color)
                {
                    AddIfValidVariableName(names, variableName);
                }
            }
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private void RefreshAvailableVariableNames()
    {
        var next = BuildAvailableVariableNames();
        var nextColor = BuildAvailableColorVariableNames();
        var variableNamesChanged = !_availableVariableNames.SequenceEqual(next, StringComparer.Ordinal);
        var colorVariableNamesChanged = !_availableColorVariableNames.SequenceEqual(nextColor, StringComparer.Ordinal);

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
            _availableVariableNames = next;
            OnPropertyChanged(nameof(AvailableVariableNames));
            OnPropertyChanged(nameof(HasAvailableVariableNames));
        }

        if (colorVariableNamesChanged)
        {
            _availableColorVariableNames = nextColor;
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
        NotifyScreenReadingComputedPropertiesChanged();
        ClearVariableSuggestionSelections();
    }

    private void ClearVariableSuggestionSelections()
    {
        SetSuggestionValue(ref _selectedSetVariableSuggestion, nameof(SelectedSetVariableSuggestion), null);
        SetSuggestionValue(ref _selectedIncDecVariableSuggestion, nameof(SelectedIncDecVariableSuggestion), null);
        SetSuggestionValue(ref _selectedConditionLeftVariableSuggestion, nameof(SelectedConditionLeftVariableSuggestion), null);
        SetSuggestionValue(ref _selectedConditionRightVariableSuggestion, nameof(SelectedConditionRightVariableSuggestion), null);
        SetSuggestionValue(ref _selectedForVariableSuggestion, nameof(SelectedForVariableSuggestion), null);
        SetSuggestionValue(ref _selectedScreenTargetColorVariableSuggestion, nameof(SelectedScreenTargetColorVariableSuggestion), null);
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

    private static void TryAddLegacySetVariableName(ISet<string> target, string legacyText)
    {
        if (string.IsNullOrWhiteSpace(legacyText))
        {
            return;
        }

        var text = legacyText.Trim();
        var equalIndex = text.IndexOf('=');
        if (equalIndex > 0)
        {
            AddIfValidVariableName(target, text[..equalIndex]);
            return;
        }

        var firstPart = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        AddIfValidVariableName(target, firstPart ?? string.Empty);
    }

    private static void AddIfValidVariableName(ISet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var token = value.Trim();
        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            token = token[1..];
        }

        if (Regex.IsMatch(token, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            target.Add(token);
        }
    }
}
