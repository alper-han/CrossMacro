
namespace CrossMacro.UI.ViewModels.Editor;

public partial class EditorViewModel
{
    private enum PostRemoveSelectionPolicy
    {
        ClearSelection,
        PreserveSurvivingSelection,
    }

    private static readonly HashSet<string> UndoSkipProperties = new(StringComparer.Ordinal)
    {
        nameof(EditorAction.DisplayName),
        nameof(EditorAction.Index),
        nameof(EditorAction.KeyName),
    };

    private void UpdateActionIndices()
    {
        for (var index = 0; index < Actions.Count; index++)
        {
            Actions[index].Index = index + 1;
        }
    }

    private static List<EditorAction> CloneActions(IEnumerable<EditorAction> actions) => actions.Select(action => action.Clone()).ToList();

    private EditorStateSnapshot CloneState() => new(CloneActions(Actions), _skipInitialZeroZero);

    private bool GetCurrentCoordinateMode(int insertionIndex)
    {
        if (TryGetCoordinateMode(SelectedAction, out var selectedMode))
        {
            return selectedMode;
        }

        for (var index = Math.Min(insertionIndex - 1, Actions.Count - 1); index >= 0; index--)
        {
            if (TryGetCoordinateMode(Actions[index], out var previousMode))
            {
                return previousMode;
            }
        }

        return Actions
            .FirstOrDefault(action => UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action))
            ?.IsAbsolute ?? true;
    }

    private static bool TryGetCoordinateMode(EditorAction? action, out bool isAbsolute)
    {
        if (action is not null && UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action))
        {
            isAbsolute = action.IsAbsolute;
            return true;
        }

        isAbsolute = true;
        return false;
    }

    private static void NormalizeCoordinateAction(EditorAction sourceAction)
    {
        if (!UsesCoordinateFields(sourceAction.Type))
        {
            return;
        }

        NormalizeCurrentPositionMouseButtonAction(sourceAction);
    }

    private static void NormalizeCurrentPositionMouseButtonAction(EditorAction action)
    {
        if (!IsCurrentPositionMouseButtonAction(action))
        {
            return;
        }

        action.X = 0;
        action.Y = 0;
        action.IsAbsolute = false;
    }

    private static void NormalizeCurrentPositionMouseButtonActionSnapshot(IEnumerable<EditorAction> actions)
    {
        foreach (var action in actions)
        {
            NormalizeCurrentPositionMouseButtonAction(action);
        }
    }

    private void RememberCurrentState()
    {
        _history.Remember(CloneState());
        UpdateDirtyState();
    }

    private void ResetPropertyEditUndoCoalescing() => _history.ResetCoalescing();

    private void SetSelectedImageSearchMatchMode(EditorImageMatchMode value)
    {
        var action = SelectedAction;
        if (action is null || action.ImageSearchMatchMode == value)
        {
            return;
        }

        if (!ShouldCoalescePropertyUndo(action, nameof(EditorAction.ImageSearchMatchMode)))
        {
            SaveUndoState(_history.LastKnownState);
        }

        action.SetImageSearchMatchMode(value);
        RememberCurrentState();
        UpdateActionListPresentation();
        OnPropertyChanged(nameof(SelectedImageSearchMatchMode));
    }

    private bool ShouldCoalescePropertyUndo(EditorAction? action, string propertyName) => _history.ShouldCoalesce(action, propertyName);

    private void OnSelectedActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        var changes = EditorPresentationChanges.For(propertyName);
        var shouldTrackUndo = propertyName is not null && !UndoSkipProperties.Contains(propertyName) && !_isRestoringState;

        if (shouldTrackUndo && !_isSynchronizingActionProperties && !ShouldCoalescePropertyUndo(sender as EditorAction, propertyName!))
        {
            SaveUndoState(_history.LastKnownState);
        }

        if (changes.NormalizeAction && sender is EditorAction selectedAction)
        {
            NormalizeSelectedActionState(selectedAction);
        }

        if (changes.Visibility)
        {
            NotifyVisibilityChanged();
        }

        if (changes.ScreenReading)
        {
            NotifyScreenReadingComputedPropertiesChanged();
        }

        if (changes.VariableNames)
        {
            RefreshAvailableVariableNames();
        }

        if (changes.ImagePreview)
        {
            _ = RefreshSelectedImageAssetPreviewAsync();
        }

        if (changes.TextInput)
        {
            OnPropertyChanged(nameof(SelectedActionDisplayText));
        }

        if (changes.KeyName && sender is EditorAction action)
        {
            if (action.KeyCode > 0)
            {
                var newKeyName = _keyCodeMapper.GetKeyName(action.KeyCode);
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
            OnPropertyChanged(nameof(SelectedActionIsAbsolute));
            OnPropertyChanged(nameof(SelectedActionIsRelative));
            OnPropertyChanged(nameof(SelectedActionIsRawRelative));
            OnPropertyChanged(nameof(SelectedActionIsLogicalRelative));
            RefreshCurrentPositionConfiguration();
        }

        if (shouldTrackUndo)
        {
            RememberCurrentState();
        }
    }

    private void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowCoordinates));
        OnPropertyChanged(nameof(ShowCoordModeToggle));
        OnPropertyChanged(nameof(ShowCurrentPositionToggle));
        OnPropertyChanged(nameof(CurrentPositionToggleLabel));
        OnPropertyChanged(nameof(ShowMouseButton));
        OnPropertyChanged(nameof(ShowImageClickButton));
        OnPropertyChanged(nameof(ShowKeyCode));
        OnPropertyChanged(nameof(ShowDelay));
        OnPropertyChanged(nameof(ShowFixedDelayInput));
        OnPropertyChanged(nameof(ShowRandomDelayOptions));
        OnPropertyChanged(nameof(ShowScrollAmount));
        OnPropertyChanged(nameof(ShowTextInput));
        OnPropertyChanged(nameof(ShowSetVariableFields));
        OnPropertyChanged(nameof(ShowClipboardGetFields));
        OnPropertyChanged(nameof(ShowCopySelectionToVariableFields));
        OnPropertyChanged(nameof(ShowMousePositionFields));
        OnPropertyChanged(nameof(ShowClipboardVariablePicker));
        OnPropertyChanged(nameof(SelectedClipboardVariableSuggestion));
        OnPropertyChanged(nameof(ShowScreenshotFields));
        OnPropertyChanged(nameof(ShowScreenshotRegionFields));
        OnPropertyChanged(nameof(ShowShellCommandFields));
        OnPropertyChanged(nameof(ShowShellStandardInputFields));
        OnPropertyChanged(nameof(ShowShellCaptureFields));
        OnPropertyChanged(nameof(ShowWindowCommandFields));
        OnPropertyChanged(nameof(ShowWindowSelectorFields));
        OnPropertyChanged(nameof(ShowWindowSearchSelectorKinds));
        OnPropertyChanged(nameof(ShowWindowFocusSelectorKinds));
        OnPropertyChanged(nameof(ShowWindowCloseSelectorKinds));
        OnPropertyChanged(nameof(ShowWindowSelectorValueField));
        OnPropertyChanged(nameof(ShowWindowActiveFieldSelector));
        OnPropertyChanged(nameof(ShowWindowCoordinateFields));
        OnPropertyChanged(nameof(ShowWindowDimensionFields));
        OnPropertyChanged(nameof(ShowWindowTimeoutField));
        OnPropertyChanged(nameof(ShowWindowOutputVariableField));
        OnPropertyChanged(nameof(ShowWindowWorkspaceField));
        OnPropertyChanged(nameof(ShowWindowAddressField));
        OnPropertyChanged(nameof(ShowIncDecFields));
        OnPropertyChanged(nameof(ShowRepeatFields));
        OnPropertyChanged(nameof(ShowConditionFields));
        OnPropertyChanged(nameof(ShowForFields));
        OnPropertyChanged(nameof(ShowForStepFields));
        OnPropertyChanged(nameof(ShowPixelColorFields));
        OnPropertyChanged(nameof(ShowWaitColorFields));
        OnPropertyChanged(nameof(ShowPixelSearchFields));
        OnPropertyChanged(nameof(ShowImageSearchFields));
        OnPropertyChanged(nameof(ShowScreenReadingColorFields));
        OnPropertyChanged(nameof(ShowScreenReadingPointFields));
        OnPropertyChanged(nameof(ScreenTargetColorSources));
        OnPropertyChanged(nameof(ShowSetVariablePicker));
        OnPropertyChanged(nameof(ShowIncDecVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
        OnPropertyChanged(nameof(SelectedConditionLeftVariableSuggestion));
        OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
        OnPropertyChanged(nameof(ShowConditionRightOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionRightColorPicker));
        OnPropertyChanged(nameof(SelectedConditionRightVariableSuggestion));
        OnPropertyChanged(nameof(ScriptConditionOperators));
        OnPropertyChanged(nameof(ConditionRightOperandHint));
        OnPropertyChanged(nameof(ShowForVariablePicker));
        OnPropertyChanged(nameof(TextInputLabel));
        OnPropertyChanged(nameof(TextInputWatermark));
        OnPropertyChanged(nameof(TextInputHint));
        OnPropertyChanged(nameof(TextInputAcceptsReturn));
        OnPropertyChanged(nameof(SkipInitialZeroZero));
        OnPropertyChanged(nameof(RequiresSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanEditSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanRemoveBlock));
        NotifyScriptArithmeticPresentationChanged();
        NotifyScreenReadingComputedPropertiesChanged();
        RefreshAvailableVariableNames();
    }

    private void SetSelectedActionCoordinateMode(bool isAbsolute, MouseCoordinateSpace relativeCoordinateSpace = MouseCoordinateSpace.RawDevice)
    {
        if (SelectedAction is null
            || (SelectedAction.IsAbsolute == isAbsolute
                && (isAbsolute || SelectedAction.CoordinateSpace == relativeCoordinateSpace)))
        {
            return;
        }

        SelectedAction.IsAbsolute = isAbsolute;
        if (!isAbsolute)
        {
            SelectedAction.CoordinateSpace = relativeCoordinateSpace;
        }

        if (!isAbsolute && SelectedAction.Type is EditorActionType.MouseMove)
        {
            SkipInitialZeroZero = true;
            RememberCurrentState();
        }
    }

    private void RefreshCurrentPositionConfiguration()
    {
        if (RequiresSkipInitialZeroZero)
        {
            if (!_skipInitialZeroZeroForcedByCurrentPosition)
            {
                _skipInitialZeroZeroBeforeCurrentPositionForce = _skipInitialZeroZero;
                _skipInitialZeroZeroForcedByCurrentPosition = true;
            }

            if (!_skipInitialZeroZero)
            {
                _skipInitialZeroZero = true;
            }
        }
        else if (_skipInitialZeroZeroForcedByCurrentPosition)
        {
            _skipInitialZeroZero = _skipInitialZeroZeroBeforeCurrentPositionForce;
            _skipInitialZeroZeroForcedByCurrentPosition = false;
        }

        NotifyVisibilityChanged();
    }

    private void NormalizeSelectedActionState(EditorAction action)
    {
        if (_isSynchronizingActionProperties)
        {
            return;
        }

        try
        {
            _isSynchronizingActionProperties = true;

            if (action.Type is not (EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp)
                && action.UseCurrentPosition)
            {
                action.UseCurrentPosition = false;
            }

            if (action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
                && action.UseCurrentPosition)
            {
                NormalizeCurrentPositionMouseButtonAction(action);
            }

            if (action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && !IsOperatorValidForOperands(action))
            {
                action.ScriptConditionOperator = ScriptConditionOperator.Equals;
            }

            if (action.Type is EditorActionType.WindowCommand)
            {
                if (action.WindowCommandMode is WindowCommandMode.Search && !WindowSearchSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = WindowSearchSelectorKinds[0];
                }
                else if (action.WindowCommandMode is WindowCommandMode.Focus && !WindowFocusSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = WindowFocusSelectorKinds[0];
                }
                else if (action.WindowCommandMode is WindowCommandMode.Close && !WindowCloseSelectorKinds.Contains(action.WindowSelectorKind, StringComparer.Ordinal))
                {
                    action.WindowSelectorKind = WindowCloseSelectorKinds[0];
                }
            }

            NormalizeScriptArithmeticForAction(action);
        }
        finally
        {
            _isSynchronizingActionProperties = false;
        }

        RefreshCurrentPositionConfiguration();
    }

    private void SaveUndoState()
    {
        SaveUndoState(CloneState());
    }

    private void SaveUndoState(EditorStateSnapshot state)
    {
        _history.Save(state);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void ClearUndoHistory()
    {
        _history.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void AddAction()
    {
        if (!IsUserAddableActionType(NewActionType))
        {
            Status = Localize("Editor_StatusAutoManagedAction");
            return;
        }

        var insertionIndex = GetInsertionIndexAfterSelection();
        if (!CanApplyScriptStructureMutation(candidate =>
            {
                candidate.Insert(insertionIndex, new EditorAction { Type = NewActionType });
                if (IsAutoManagedBlockStartAction(NewActionType))
                {
                    candidate.Insert(insertionIndex + 1, new EditorAction { Type = EditorActionType.BlockEnd });
                }
            }))
        {
            return;
        }

        SaveUndoState();

        var isCoordinateAction = UsesCoordinateFields(NewActionType);
        var coordinateMode = !isCoordinateAction || GetCurrentCoordinateMode(insertionIndex);

        var action = new EditorAction
        {
            Type = NewActionType,
            IsAbsolute = coordinateMode,
            DelayMs = NewActionType is EditorActionType.Delay ? 100 : 0,
            UseRandomDelay = false,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            ScrollAmount = NewActionType is EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal ? 1 : 0,
            Text = NewActionType is EditorActionType.ClipboardSet ? "clipboard text" : string.Empty,
            ScriptVariableName = NewActionType is EditorActionType.ClipboardGet or EditorActionType.CopySelectionToVariable ? "clipboardText" : "i",
            ClipboardCopyShortcut = ClipboardCopyShortcut.CtrlC,
            MousePositionXVariableName = "mouse_x",
            MousePositionYVariableName = "mouse_y",
            ShellCommand = NewActionType is EditorActionType.ShellCommand ? "echo hello" : string.Empty,
            ScreenshotCopyToClipboard = NewActionType is EditorActionType.Screenshot,
            ShellStandardInput = string.Empty,
            ShellExitCodeVariableName = "exit_code",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "stderr",
            WindowCommandMode = WindowCommandMode.Active,
            WindowActiveField = "title",
            WindowOutputVariable = "windowResult",
            WindowTimeoutMs = 5000,
            WindowWidth = 1280,
            WindowHeight = 720,
            ScreenWidth = EditorActionScreenReadingPayload.DefaultPointScreenWidth,
            ScreenHeight = EditorActionScreenReadingPayload.DefaultPointScreenHeight,
            ScreenColorHex = EditorActionScreenReadingPayload.DefaultColorHex,
            ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex,
            ScreenTargetColorVariableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName,
            ScreenColorVariableName = EditorActionScreenReadingPayload.DefaultColorVariableName,
            ScreenFoundVariableName = EditorActionScreenReadingPayload.DefaultFoundVariableName,
            ScreenFoundXVariableName = EditorActionScreenReadingPayload.DefaultFoundXVariableName,
            ScreenFoundYVariableName = EditorActionScreenReadingPayload.DefaultFoundYVariableName,
            ScreenTimeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs,
            ScreenTolerance = EditorActionScreenReadingPayload.DefaultTolerance,
            ImageAssetName = NewActionType is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage
                && ImageAssetNames.Count > 0
                    ? ImageAssetNames[0]
                    : string.Empty,
            ImageSearchSimilarity = EditorActionScreenReadingPayload.DefaultImageSearchSimilarity,
            Button = MacroMouseButton.Left,
        };

        if (EditorActionScreenReadingPayload.TryCreateDefault(NewActionType, out var screenReadingPayload))
        {
            action.ApplyScreenReadingPayload(screenReadingPayload);

            if (NewActionType is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage
                && ImageAssetNames.Count > 0)
            {
                action.ImageAssetName = ImageAssetNames[0];
            }
        }

        Actions.Insert(insertionIndex, action);
        if (IsAutoManagedBlockStartAction(NewActionType))
        {
            Actions.Insert(insertionIndex + 1, new EditorAction
            {
                Type = EditorActionType.BlockEnd,
            });
        }

        SelectedAction = action;
        Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusAddedAction"), _actionDisplayFormatter.Format(action));
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanAddToPlayback));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void AddCurrentPositionClick()
    {
        var insertionIndex = GetInsertionIndexAfterSelection();
        if (!CanApplyScriptStructureMutation(candidate =>
            candidate.Insert(insertionIndex, new EditorAction { Type = EditorActionType.MouseClick })))
        {
            return;
        }

        SaveUndoState();

        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            IsAbsolute = false,
            UseCurrentPosition = true,
            Button = MacroMouseButton.Left,
            X = 0,
            Y = 0,
        };

        Actions.Insert(insertionIndex, action);
        SkipInitialZeroZero = true;
        SelectedAction = action;
        Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusAddedAction"), _actionDisplayFormatter.Format(action));
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void InsertElseBlock()
    {
        if ((SelectedAction?.Type) is not EditorActionType.IfBlockStart)
        {
            Status = Localize("Editor_StatusSelectIfBlockFirst");
            return;
        }

        var ifStartIndex = Actions.IndexOf(SelectedAction);
        if (ifStartIndex < 0 || !TryFindMatchingBlockEnd(ifStartIndex, out var ifBlockEndIndex))
        {
            Status = Localize("Editor_StatusSelectIfBlockFirst");
            return;
        }

        if (ifBlockEndIndex + 1 < Actions.Count
&& Actions[ifBlockEndIndex + 1].Type is EditorActionType.ElseBlockStart)
        {
            Status = Localize("Editor_StatusElseAlreadyExists");
            return;
        }

        SaveUndoState();

        var elseStartAction = new EditorAction
        {
            Type = EditorActionType.ElseBlockStart,
        };
        var elseEndAction = new EditorAction
        {
            Type = EditorActionType.BlockEnd,
        };

        Actions.Insert(ifBlockEndIndex + 1, elseStartAction);
        Actions.Insert(ifBlockEndIndex + 2, elseEndAction);
        SelectedAction = elseStartAction;

        Status = Localize("Editor_StatusInsertedElseBlock");
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void RemoveBlock()
    {
        if (!TryGetSelectedBlockRange(out var startIndex, out var endIndex))
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate =>
            {
                for (var index = endIndex; index >= startIndex; index--)
                {
                    candidate.RemoveAt(index);
                }
            }))
        {
            return;
        }

        SaveUndoState();
        for (var index = endIndex; index >= startIndex; index--)
        {
            Actions.RemoveAt(index);
        }

        if (Actions.Count > 0)
        {
            SelectedAction = Actions[Math.Min(startIndex, Actions.Count - 1)];
        }
        else
        {
            SelectedAction = null;
        }

        Status = Localize("Editor_StatusRemovedBlock");
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void RemoveAction()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index < 0)
        {
            return;
        }

        RemoveActionsAtIndices([index], "Editor_StatusRemovedAction", PostRemoveSelectionPolicy.ClearSelection);
    }

    public void RemoveSelectedActions()
    {
        var indices = SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .Order()
            .ToArray();
        if (indices.Length is 0)
        {
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusRemovedSelectedActions", PostRemoveSelectionPolicy.ClearSelection);
    }

    public void DeleteHiddenEvents()
    {
        var indices = Actions
            .Select((action, index) => new { action, index })
            .Where(item => EditorActionListMetadata.IsHidden(item.action, HideMouseMoves, HideShortWaits))
            .Select(item => item.index)
            .ToArray();
        if (indices.Length is 0)
        {
            Status = Localize("Editor_StatusNoHiddenEventsToDelete");
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusDeletedHiddenEvents", PostRemoveSelectionPolicy.PreserveSurvivingSelection);
    }

    public void MoveUp()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index <= 0)
        {
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate => MoveAction(candidate, index, index - 1)))
        {
            return;
        }

        SaveUndoState();
        var action = SelectedAction;
        Actions.Move(index, index - 1);
        SelectedAction = action;
        Status = Localize("Editor_StatusMovedActionUp");
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void MoveDown()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index < 0 || index >= Actions.Count - 1)
        {
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate => MoveAction(candidate, index, index + 1)))
        {
            return;
        }

        SaveUndoState();
        var action = SelectedAction;
        Actions.Move(index, index + 1);
        SelectedAction = action;
        Status = Localize("Editor_StatusMovedActionDown");
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void MoveSelectedActionsUp()
    {
        var indices = GetNormalizedSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        if (indices[0] <= 0)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate => MoveIndicesUp(candidate, indices)))
        {
            return;
        }

        SaveUndoState();
        foreach (var action in indices.Select(index => Actions[index]).ToArray())
        {
            var currentIndex = Actions.IndexOf(action);
            Actions.Move(currentIndex, currentIndex - 1);
        }

        SetSelectedActionUnderlyingIndices(indices.Select(index => index - 1));
        SelectPrimaryActionFromUnderlyingSelection();
        Status = Localize("Editor_StatusMovedSelectedActionsUp");
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void MoveSelectedActionsDown()
    {
        var indices = GetNormalizedSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        if (indices[^1] >= Actions.Count - 1)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate => MoveIndicesDown(candidate, indices)))
        {
            return;
        }

        SaveUndoState();
        foreach (var action in indices.Select(index => Actions[index]).Reverse().ToArray())
        {
            var currentIndex = Actions.IndexOf(action);
            Actions.Move(currentIndex, currentIndex + 1);
        }

        SetSelectedActionUnderlyingIndices(indices.Select(index => index + 1));
        SelectPrimaryActionFromUnderlyingSelection();
        Status = Localize("Editor_StatusMovedSelectedActionsDown");
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void DuplicateAction()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index < 0)
        {
            return;
        }

        if (!CanApplyScriptStructureMutation(candidate => candidate.Insert(index + 1, candidate[index].Clone())))
        {
            return;
        }

        SaveUndoState();
        var clone = SelectedAction.Clone();
        Actions.Insert(index + 1, clone);
        SelectedAction = clone;
        Status = Localize("Editor_StatusDuplicatedAction");
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void DuplicateSelectedActions()
    {
        var indices = GetNormalizedSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        var insertionIndex = indices[^1] + 1;
        if (!CanApplyScriptStructureMutation(candidate => InsertClones(candidate, indices, insertionIndex)))
        {
            return;
        }

        SaveUndoState();
        var clones = indices.Select(index => Actions[index].Clone()).ToArray();
        for (var offset = 0; offset < clones.Length; offset++)
        {
            Actions.Insert(insertionIndex + offset, clones[offset]);
        }

        SetSelectedActionUnderlyingIndices(Enumerable.Range(insertionIndex, clones.Length));
        SelectPrimaryActionFromUnderlyingSelection();
        Status = Localize("Editor_StatusDuplicatedSelectedActions");
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void ClearAll()
    {
        if (Actions.Count is 0)
        {
            return;
        }

        SaveUndoState();
        Actions.Clear();
        SetLoadWarnings([]);
        SelectedAction = null;
        Status = Localize("Editor_StatusClearedAllActions");
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    private void RemoveActionsAtIndices(
        IReadOnlyCollection<int> indices,
        string statusKey,
        PostRemoveSelectionPolicy postRemoveSelectionPolicy)
    {
        var orderedIndices = NormalizeActionIndices(indices);
        if (orderedIndices.Length is 0)
        {
            return;
        }

        var selectedActionsBeforeRemoval = postRemoveSelectionPolicy is PostRemoveSelectionPolicy.PreserveSurvivingSelection
            ? GetSelectedActions()
            : [];

        if (!CanApplyScriptStructureMutation(candidate => RemoveIndicesDescending(candidate, orderedIndices)))
        {
            return;
        }

        SaveUndoState();
        _isBatchUpdatingActions = true;
        try
        {
            RemoveIndicesDescending(Actions, orderedIndices);
        }
        finally
        {
            _isBatchUpdatingActions = false;
        }

        RefreshActionCollectionState();
        ApplyPostRemoveSelection(selectedActionsBeforeRemoval, postRemoveSelectionPolicy);
        Status = Localize(statusKey);
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    private int[] GetNormalizedSelectedActionIndices()
    {
        return NormalizeActionIndices(SelectedActionUnderlyingIndices);
    }

    private int[] NormalizeActionIndices(IEnumerable<int> indices) => EditorSelection.Normalize(indices, Actions.Count);

    private void SetSelectedActionUnderlyingIndices(IEnumerable<int> indices)
    {
        _selection.ReplaceIndices(indices);
        NotifySelectedActionsChanged();
    }

    private void ApplyPostRemoveSelection(
        IReadOnlyCollection<EditorAction> selectedActionsBeforeRemoval,
        PostRemoveSelectionPolicy postRemoveSelectionPolicy)
    {
        switch (postRemoveSelectionPolicy)
        {
            case PostRemoveSelectionPolicy.ClearSelection:
                ClearActionSelection();
                return;
            case PostRemoveSelectionPolicy.PreserveSurvivingSelection:
                PreserveSurvivingSelection(selectedActionsBeforeRemoval);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(postRemoveSelectionPolicy), postRemoveSelectionPolicy, message: null);
        }
    }

    private void PreserveSurvivingSelection(IReadOnlyCollection<EditorAction> selectedActionsBeforeRemoval)
    {
        if (selectedActionsBeforeRemoval.Count is 0)
        {
            ClearActionSelection();
            return;
        }

        var survivingSelectedIndices = selectedActionsBeforeRemoval
            .Select(action => Actions.IndexOf(action))
            .Where(index => index >= 0)
            .ToArray();

        if (survivingSelectedIndices.Length is 0)
        {
            ClearActionSelection();
            return;
        }

        SetSelectedActionUnderlyingIndices(survivingSelectedIndices);
        SelectPrimaryActionFromUnderlyingSelection();
    }

    private void ClearActionSelection()
    {
        SelectedAction = null;
        SetSelectedActionUnderlyingIndices([]);
        SyncSelectedActionListItem();
    }

    private void RestoreStateSnapshot(EditorStateSnapshot state)
    {
        _isBatchUpdatingActions = true;
        try
        {
            Actions.Clear();
            foreach (var action in state.Actions)
            {
                Actions.Add(action);
            }
        }
        finally
        {
            _isBatchUpdatingActions = false;
        }

        SkipInitialZeroZero = state.SkipInitialZeroZero;
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        ResetPropertyEditUndoCoalescing();
        _isRestoringState = true;
        try
        {
            var previousState = _history.Undo(CloneState());
            RestoreStateSnapshot(previousState);

            SelectedAction = Actions.FirstOrDefault();
            Status = Localize("Editor_StatusUndone");

            RefreshActionCollectionState();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
            OnPropertyChanged(nameof(CanRunTest));
            RememberCurrentState();
        }
        finally
        {
            _isRestoringState = false;
            UpdateDirtyState();
        }
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        ResetPropertyEditUndoCoalescing();
        _isRestoringState = true;
        try
        {
            var nextState = _history.Redo(CloneState());
            RestoreStateSnapshot(nextState);

            SelectedAction = Actions.FirstOrDefault();
            Status = Localize("Editor_StatusRedone");

            RefreshActionCollectionState();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
            OnPropertyChanged(nameof(CanRunTest));
            RememberCurrentState();
        }
        finally
        {
            _isRestoringState = false;
            UpdateDirtyState();
        }
    }

    private bool TryFindMatchingBlockEnd(int startIndex, out int endIndex)
    {
        endIndex = -1;
        if (startIndex < 0 || startIndex >= Actions.Count || !IsScriptBlockStartAction(Actions[startIndex].Type))
        {
            return false;
        }

        var depth = 0;
        for (var index = startIndex; index < Actions.Count; index++)
        {
            var actionType = Actions[index].Type;
            if (IsScriptBlockStartAction(actionType))
            {
                depth++;
            }
            else if (actionType is EditorActionType.BlockEnd)
            {
                depth--;
                if (depth is 0)
                {
                    endIndex = index;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryFindMatchingBlockStart(int endIndex, out int startIndex)
    {
        startIndex = -1;
        if (endIndex < 0 || endIndex >= Actions.Count || Actions[endIndex].Type is not EditorActionType.BlockEnd)
        {
            return false;
        }

        var blockStack = new Stack<int>();
        for (var index = 0; index <= endIndex; index++)
        {
            var actionType = Actions[index].Type;
            if (IsScriptBlockStartAction(actionType))
            {
                blockStack.Push(index);
                continue;
            }

            if (actionType is not EditorActionType.BlockEnd)
            {
                continue;
            }

            if (blockStack.Count is 0)
            {
                return false;
            }

            var matchedStartIndex = blockStack.Pop();
            if (index == endIndex)
            {
                startIndex = matchedStartIndex;
                return true;
            }
        }

        return false;
    }

    private int GetInsertionIndexAfterSelection()
    {
        if (SelectedAction is null)
        {
            return Actions.Count;
        }

        var selectedIndex = Actions.IndexOf(SelectedAction);
        return selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;
    }

    private bool TryGetSelectedBlockRange(out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;

        if (SelectedAction is null)
        {
            return false;
        }

        var selectedIndex = Actions.IndexOf(SelectedAction);
        if (selectedIndex < 0)
        {
            return false;
        }

        if (IsScriptBlockStartAction(SelectedAction.Type))
        {
            startIndex = selectedIndex;
            if (!TryFindMatchingBlockEnd(selectedIndex, out endIndex))
            {
                return false;
            }

            ExtendIfRangeWithElse(startIndex, ref endIndex);
            return true;
        }

        if (SelectedAction.Type is not EditorActionType.BlockEnd)
        {
            return false;
        }

        endIndex = selectedIndex;
        if (!TryFindMatchingBlockStart(selectedIndex, out startIndex))
        {
            return false;
        }

        ExtendIfRangeWithElse(startIndex, ref endIndex);
        return true;
    }

    private void ExtendIfRangeWithElse(int startIndex, ref int endIndex)
    {
        if (startIndex < 0
            || startIndex >= Actions.Count
            || Actions[startIndex].Type is not EditorActionType.IfBlockStart)
        {
            return;
        }

        var elseIndex = endIndex + 1;
        if (elseIndex >= Actions.Count || Actions[elseIndex].Type is not EditorActionType.ElseBlockStart)
        {
            return;
        }

        if (TryFindMatchingBlockEnd(elseIndex, out var elseEndIndex))
        {
            endIndex = elseEndIndex;
        }
    }

    private bool CanApplyScriptStructureMutation(Action<List<EditorAction>> mutation)
    {
        var candidate = CloneActions(Actions);
        mutation(candidate);
        if (ScriptBlockStructureValidator.Validate(candidate).IsValid)
        {
            return true;
        }

        Status = Localize("Editor_StatusOperationBlocked");
        return false;
    }

    private static void MoveAction(List<EditorAction> actions, int sourceIndex, int destinationIndex)
    {
        var action = actions[sourceIndex];
        actions.RemoveAt(sourceIndex);
        actions.Insert(destinationIndex, action);
    }

    private static void MoveIndicesUp(List<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.Order())
        {
            MoveAction(actions, index, index - 1);
        }
    }

    private static void MoveIndicesDown(List<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.OrderDescending())
        {
            MoveAction(actions, index, index + 1);
        }
    }

    private static void InsertClones(List<EditorAction> actions, IReadOnlyList<int> indices, int insertionIndex)
    {
        var clones = indices
            .Select(index => actions[index].Clone())
            .ToArray();
        for (var offset = 0; offset < clones.Length; offset++)
        {
            actions.Insert(insertionIndex + offset, clones[offset]);
        }
    }

    private static void RemoveIndicesDescending(IList<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.OrderDescending())
        {
            actions.RemoveAt(index);
        }
    }

    private static bool IsScriptBlockStartAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.ElseBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

}
