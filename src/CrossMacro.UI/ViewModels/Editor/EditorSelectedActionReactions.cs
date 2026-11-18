namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Reacts to edits of the selected action: records undo steps, keeps dependent fields valid, refreshes
/// the presentation and applies the coordinate-mode and image-match-mode choices.
/// </summary>
internal sealed class EditorSelectedActionReactions(
    EditorViewModel owner,
    EditorUndoRedo undo,
    EditorSkipInitialZeroZero skipInitialZeroZero,
    IKeyCodeMapper keyCodeMapper)
{
    private static readonly HashSet<string> UndoSkipProperties = new(StringComparer.Ordinal)
    {
        nameof(EditorAction.Index),
        nameof(EditorAction.KeyName),
    };

    internal void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        var changes = EditorPresentationChanges.For(propertyName);
        var shouldTrackUndo = propertyName is not null && !UndoSkipProperties.Contains(propertyName) && !owner.IsRestoringState;

        if (shouldTrackUndo && !owner.IsSynchronizingActionProperties && !undo.ShouldCoalesce(sender as EditorAction, propertyName!))
        {
            undo.SaveBaseline();
        }

        if (changes.NormalizeAction && sender is EditorAction selectedAction)
        {
            NormalizeState(selectedAction);
        }

        if (changes.Visibility)
        {
            owner.NotifyVisibilityChanged();
        }

        if (changes.ScreenReading)
        {
            owner.NotifyScreenReadingChanged();
        }

        if (changes.VariableNames)
        {
            owner.RefreshAvailableVariableNames();
        }

        if (changes.ImagePreview)
        {
            _ = owner.ImagePreview.RefreshAsync();
        }

        if (changes.TextInput)
        {
            owner.NotifySelectedActionDisplayTextChanged();
        }

        if (changes.KeyName && sender is EditorAction action)
        {
            if (action.KeyCode > 0)
            {
                var newKeyName = keyCodeMapper.GetKeyName(action.KeyCode);
                if (!string.Equals(action.KeyName, newKeyName, StringComparison.Ordinal))
                {
                    action.KeyName = newKeyName;
                }
            }
            else
            {
                action.KeyName = null;
            }
        }

        if (changes.Coordinates && sender is EditorAction coordAction)
        {
            NormalizeCoordinateAction(coordAction);
            owner.NotifyCoordinateModeChanged();
            RefreshCurrentPosition();
        }

        if (shouldTrackUndo)
        {
            owner.RememberCurrentState();
        }
    }


    internal void NormalizeState(EditorAction action)
    {
        if (owner.IsSynchronizingActionProperties)
        {
            return;
        }

        try
        {
            owner.IsSynchronizingActionProperties = true;

            if (action.Type is not (EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp)
                && action.UseCurrentPosition)
            {
                action.UseCurrentPosition = false;
            }

            if (action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
                && action.UseCurrentPosition)
            {
                EditorActionCoordinateRules.NormalizeCurrentPositionMouseButton(action);
            }

            if (action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && !owner.ConditionRules.IsOperatorValid(action))
            {
                action.ScriptConditionOperator = ScriptConditionOperator.Equals;
            }

            if (action.Type is EditorActionType.WindowCommand)
            {
                if (action.WindowCommandMode is WindowCommandMode.Search && !EditorViewModel.WindowSearchSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = EditorViewModel.WindowSearchSelectorKinds[0];
                }
                else if (action.WindowCommandMode is WindowCommandMode.Focus && !EditorViewModel.WindowFocusSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = EditorViewModel.WindowFocusSelectorKinds[0];
                }
                else if (action.WindowCommandMode is WindowCommandMode.Close && !EditorViewModel.WindowCloseSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = EditorViewModel.WindowCloseSelectorKinds[0];
                }
            }

            owner.Arithmetic.NormalizeForAction(action);
        }
        finally
        {
            owner.IsSynchronizingActionProperties = false;
        }

        RefreshCurrentPosition();
    }


    internal void SetCoordinateMode(bool isAbsolute, MouseCoordinateSpace relativeCoordinateSpace = MouseCoordinateSpace.RawDevice)
    {
        if (owner.SelectedAction is null
            || (owner.SelectedAction.IsAbsolute == isAbsolute
                && (isAbsolute || owner.SelectedAction.CoordinateSpace == relativeCoordinateSpace)))
        {
            return;
        }

        owner.SelectedAction.IsAbsolute = isAbsolute;
        if (!isAbsolute)
        {
            owner.SelectedAction.CoordinateSpace = relativeCoordinateSpace;
        }

        if (!isAbsolute && owner.SelectedAction.Type is EditorActionType.MouseMove)
        {
            owner.SkipInitialZeroZero = true;
            owner.RememberCurrentState();
        }
    }


    internal void SetImageMatchMode(EditorImageMatchMode value)
    {
        var action = owner.SelectedAction;
        if (action is null || action.ImageSearchMatchMode == value)
        {
            return;
        }

        if (!undo.ShouldCoalesce(action, nameof(EditorAction.ImageSearchMatchMode)))
        {
            undo.SaveBaseline();
        }

        action.SetImageSearchMatchMode(value);
        owner.RememberCurrentState();
        owner.UpdateActionListPresentation();
        owner.NotifyImageMatchModeChanged();
    }


    internal void RefreshCurrentPosition()
    {
        skipInitialZeroZero.Refresh();
        owner.NotifyVisibilityChanged();
    }

    private static void NormalizeCoordinateAction(EditorAction sourceAction)
    {
        if (EditorActionCoordinateRules.UsesCoordinates(sourceAction.Type))
        {
            EditorActionCoordinateRules.NormalizeCurrentPositionMouseButton(sourceAction);
        }
    }
}
