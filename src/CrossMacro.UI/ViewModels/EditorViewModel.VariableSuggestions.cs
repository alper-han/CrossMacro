
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
                case EditorActionType.ShellCommand when action.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput:
                    AddIfValidVariableName(names, action.ShellExitCodeVariableName);
                    AddIfValidVariableName(names, action.ShellStandardOutputVariableName);
                    AddIfValidVariableName(names, action.ShellStandardErrorVariableName);
                    break;
                case EditorActionType.WindowCommand when action.WindowCommandMode is WindowCommandMode.Active or WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.WorkspaceGet:
                    AddIfValidVariableName(names, action.WindowOutputVariable);
                    break;
                case EditorActionType.ImageSearch:
                case EditorActionType.ImageClick:
                case EditorActionType.WaitImage:
                    AddIfValidVariableName(names, action.ScreenFoundVariableName);
                    AddIfValidVariableName(names, action.ScreenFoundXVariableName);
                    AddIfValidVariableName(names, action.ScreenFoundYVariableName);
                    break;
            }

            if (action.TryGetScreenReadingPayload(out var screenReadingPayload))
            {
                foreach (var variableName in screenReadingPayload.OutputVariableNames)
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

            foreach (var variableName in screenReadingPayload.OutputVariableNames
                .Where(name => screenReadingPayload.GetOutputVariableRole(name) is EditorActionScreenReadingVariableRole.Color))
            {
                AddIfValidVariableName(names, variableName);
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
        SetSuggestionValue(ref _selectedSetVariableSuggestion, nameof(SelectedSetVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedIncDecVariableSuggestion, nameof(SelectedIncDecVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionLeftVariableSuggestion, nameof(SelectedConditionLeftVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedConditionRightVariableSuggestion, nameof(SelectedConditionRightVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedForVariableSuggestion, nameof(SelectedForVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedClipboardVariableSuggestion, nameof(SelectedClipboardVariableSuggestion), value: null);
        SetSuggestionValue(ref _selectedScreenTargetColorVariableSuggestion, nameof(SelectedScreenTargetColorVariableSuggestion), value: null);
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
        if (token.StartsWith('$'))
        {
            token = token[1..];
        }

        if (VariableNameRegex().IsMatch(token))
        {
            target.Add(token);
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex VariableNameRegex();
}
