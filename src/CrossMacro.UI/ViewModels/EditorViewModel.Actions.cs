using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private enum PostRemoveSelectionPolicy
    {
        ClearSelection,
        PreserveSurvivingSelection
    }

    private static readonly HashSet<string> UndoSkipProperties = new()
    {
        nameof(EditorAction.DisplayName),
        nameof(EditorAction.Index),
        nameof(EditorAction.KeyName)
    };

    private void UpdateActionIndices()
    {
        for (var index = 0; index < Actions.Count; index++)
        {
            Actions[index].Index = index + 1;
        }
    }

    private static List<EditorAction> CloneState(IEnumerable<EditorAction> actions)
    {
        return actions.Select(action => action.Clone()).ToList();
    }

    private static bool AreActionsEquivalent(EditorAction left, EditorAction right)
    {
        return left.Type == right.Type
            && left.X == right.X
            && left.Y == right.Y
            && left.IsAbsolute == right.IsAbsolute
            && left.Button == right.Button
            && left.KeyCode == right.KeyCode
            && left.DelayMs == right.DelayMs
            && left.UseRandomDelay == right.UseRandomDelay
            && left.RandomDelayMinMs == right.RandomDelayMinMs
            && left.RandomDelayMaxMs == right.RandomDelayMaxMs
            && left.UseCurrentPosition == right.UseCurrentPosition
            && left.ScrollAmount == right.ScrollAmount
            && string.Equals(left.KeyName, right.KeyName, StringComparison.Ordinal)
            && string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            && string.Equals(left.ScriptVariableName, right.ScriptVariableName, StringComparison.Ordinal)
            && left.ScriptValueType == right.ScriptValueType
            && string.Equals(left.ScriptValue, right.ScriptValue, StringComparison.Ordinal)
            && left.ScriptNumericSourceType == right.ScriptNumericSourceType
            && string.Equals(left.ScriptNumericValue, right.ScriptNumericValue, StringComparison.Ordinal)
            && left.ScriptLeftOperandType == right.ScriptLeftOperandType
            && string.Equals(left.ScriptLeftOperand, right.ScriptLeftOperand, StringComparison.Ordinal)
            && left.ScriptConditionOperator == right.ScriptConditionOperator
            && left.ScriptRightOperandType == right.ScriptRightOperandType
            && string.Equals(left.ScriptRightOperand, right.ScriptRightOperand, StringComparison.Ordinal)
            && string.Equals(left.ForVariableName, right.ForVariableName, StringComparison.Ordinal)
            && left.ForStartType == right.ForStartType
            && string.Equals(left.ForStartValue, right.ForStartValue, StringComparison.Ordinal)
            && left.ForEndType == right.ForEndType
            && string.Equals(left.ForEndValue, right.ForEndValue, StringComparison.Ordinal)
            && left.ForHasStep == right.ForHasStep
            && left.ForStepType == right.ForStepType
            && string.Equals(left.ForStepValue, right.ForStepValue, StringComparison.Ordinal)
            && left.PreferLegacyScriptText == right.PreferLegacyScriptText;
    }

    private static bool AreStatesEquivalent(IReadOnlyList<EditorAction> left, IReadOnlyList<EditorAction> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!AreActionsEquivalent(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

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
        if (action != null && UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action))
        {
            isAbsolute = action.IsAbsolute;
            return true;
        }

        isAbsolute = true;
        return false;
    }

    private void NormalizeCoordinateAction(EditorAction sourceAction)
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
        _lastKnownState = CloneState(Actions);
    }

    private void ResetPropertyEditUndoCoalescing()
    {
        _lastPropertyEditAction = null;
        _lastPropertyEditName = null;
        _lastPropertyEditUndoAt = DateTimeOffset.MinValue;
    }

    private bool ShouldCoalescePropertyUndo(EditorAction? action, string propertyName)
    {
        var now = DateTimeOffset.UtcNow;
        var shouldCoalesce =
            action != null
            && ReferenceEquals(action, _lastPropertyEditAction)
            && string.Equals(propertyName, _lastPropertyEditName, StringComparison.Ordinal)
            && now - _lastPropertyEditUndoAt <= PropertyEditUndoCoalesceWindow;

        _lastPropertyEditAction = action;
        _lastPropertyEditName = propertyName;
        _lastPropertyEditUndoAt = now;

        return shouldCoalesce;
    }

    private void OnSelectedActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        var shouldTrackUndo = propertyName != null && !UndoSkipProperties.Contains(propertyName) && !_isRestoringState;

        if (shouldTrackUndo && !_isSynchronizingActionProperties && !ShouldCoalescePropertyUndo(sender as EditorAction, propertyName!))
        {
            SaveUndoState(_lastKnownState);
        }

        if (sender is EditorAction selectedAction
            && e.PropertyName is nameof(EditorAction.Type) or nameof(EditorAction.UseRandomDelay) or nameof(EditorAction.UseCurrentPosition))
        {
            NormalizeSelectedActionState(selectedAction);
        }

        if (e.PropertyName == nameof(EditorAction.Type)
            || e.PropertyName == nameof(EditorAction.UseRandomDelay)
            || e.PropertyName == nameof(EditorAction.UseCurrentPosition)
            || e.PropertyName == nameof(EditorAction.ForHasStep)
            || e.PropertyName == nameof(EditorAction.ScriptValueType)
            || e.PropertyName == nameof(EditorAction.ScriptNumericSourceType)
            || e.PropertyName == nameof(EditorAction.ScriptLeftOperandType)
            || e.PropertyName == nameof(EditorAction.ScriptRightOperandType)
            || e.PropertyName == nameof(EditorAction.ForStartType)
            || e.PropertyName == nameof(EditorAction.ForEndType)
            || e.PropertyName == nameof(EditorAction.ForStepType))
        {
            NotifyVisibilityChanged();
        }

        if (e.PropertyName is nameof(EditorAction.Type)
            or nameof(EditorAction.Text)
            or nameof(EditorAction.ScriptVariableName)
            or nameof(EditorAction.ForVariableName)
            or nameof(EditorAction.ScriptValue)
            or nameof(EditorAction.ScriptNumericValue)
            or nameof(EditorAction.ScriptLeftOperand)
            or nameof(EditorAction.ScriptRightOperand)
            or nameof(EditorAction.ForStartValue)
            or nameof(EditorAction.ForEndValue)
            or nameof(EditorAction.ForStepValue))
        {
            RefreshAvailableVariableNames();
        }

        if (e.PropertyName is nameof(EditorAction.Type) or nameof(EditorAction.Text))
        {
            OnPropertyChanged(nameof(SelectedActionDisplayText));
        }

        if (e.PropertyName == nameof(EditorAction.KeyCode) && sender is EditorAction action)
        {
            if (action.KeyCode > 0)
            {
                var newKeyName = _keyCodeMapper.GetKeyName(action.KeyCode);
                if (action.KeyName != newKeyName)
                {
                    action.KeyName = newKeyName;
                }
            }
            else
            {
                action.KeyName = null;
            }
        }

        if (e.PropertyName == nameof(EditorAction.IsAbsolute) && sender is EditorAction coordAction)
        {
            NormalizeCoordinateAction(coordAction);
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
        OnPropertyChanged(nameof(ShowKeyCode));
        OnPropertyChanged(nameof(ShowDelay));
        OnPropertyChanged(nameof(ShowFixedDelayInput));
        OnPropertyChanged(nameof(ShowRandomDelayOptions));
        OnPropertyChanged(nameof(ShowScrollAmount));
        OnPropertyChanged(nameof(ShowTextInput));
        OnPropertyChanged(nameof(ShowSetVariableFields));
        OnPropertyChanged(nameof(ShowIncDecFields));
        OnPropertyChanged(nameof(ShowRepeatFields));
        OnPropertyChanged(nameof(ShowConditionFields));
        OnPropertyChanged(nameof(ShowForFields));
        OnPropertyChanged(nameof(ShowForStepFields));
        OnPropertyChanged(nameof(ShowSetVariablePicker));
        OnPropertyChanged(nameof(ShowIncDecVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
        OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
        OnPropertyChanged(nameof(ShowForVariablePicker));
        OnPropertyChanged(nameof(TextInputLabel));
        OnPropertyChanged(nameof(TextInputWatermark));
        OnPropertyChanged(nameof(TextInputHint));
        OnPropertyChanged(nameof(TextInputAcceptsReturn));
        OnPropertyChanged(nameof(SkipInitialZeroZero));
        OnPropertyChanged(nameof(RequiresSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanEditSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanRemoveBlock));
        RefreshAvailableVariableNames();
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
        }
        finally
        {
            _isSynchronizingActionProperties = false;
        }

        RefreshCurrentPositionConfiguration();
    }

    private void SaveUndoState()
    {
        SaveUndoState(CloneState(Actions));
    }

    private void SaveUndoState(IReadOnlyList<EditorAction> state)
    {
        if (_undoStack.Count == 0 || !AreStatesEquivalent(_undoStack.Peek(), state))
        {
            _undoStack.Push(state.Select(action => action.Clone()).ToList());
            TrimUndoStack();
        }

        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void TrimUndoStack()
    {
        if (_undoStack.Count <= UndoStackLimit)
        {
            return;
        }

        var newestToOldest = _undoStack.Take(UndoStackLimit).ToArray();
        _undoStack.Clear();
        for (var index = newestToOldest.Length - 1; index >= 0; index--)
        {
            _undoStack.Push(newestToOldest[index]);
        }
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
        var coordinateMode = isCoordinateAction ? GetCurrentCoordinateMode(insertionIndex) : true;

        var action = new EditorAction
        {
            Type = NewActionType,
            IsAbsolute = coordinateMode,
            DelayMs = NewActionType == EditorActionType.Delay ? 100 : 0,
            UseRandomDelay = false,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            ScrollAmount = NewActionType is EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal ? 1 : 0
        };

        Actions.Insert(insertionIndex, action);
        if (IsAutoManagedBlockStartAction(NewActionType))
        {
            Actions.Insert(insertionIndex + 1, new EditorAction
            {
                Type = EditorActionType.BlockEnd
            });
        }

        SelectedAction = action;
        Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusAddedAction"), _actionDisplayFormatter.Format(action));
        OnPropertyChanged(nameof(HasActions));
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
            Button = MouseButton.Left,
            X = 0,
            Y = 0
        };

        Actions.Insert(insertionIndex, action);
        SkipInitialZeroZero = true;
        SelectedAction = action;
        Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusAddedAction"), _actionDisplayFormatter.Format(action));
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void InsertElseBlock()
    {
        if (SelectedAction?.Type != EditorActionType.IfBlockStart)
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
            && Actions[ifBlockEndIndex + 1].Type == EditorActionType.ElseBlockStart)
        {
            Status = Localize("Editor_StatusElseAlreadyExists");
            return;
        }

        SaveUndoState();

        var elseStartAction = new EditorAction
        {
            Type = EditorActionType.ElseBlockStart
        };
        var elseEndAction = new EditorAction
        {
            Type = EditorActionType.BlockEnd
        };

        Actions.Insert(ifBlockEndIndex + 1, elseStartAction);
        Actions.Insert(ifBlockEndIndex + 2, elseEndAction);
        SelectedAction = elseStartAction;

        Status = Localize("Editor_StatusInsertedElseBlock");
        OnPropertyChanged(nameof(HasActions));
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
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void RemoveAction()
    {
        if (SelectedAction == null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index < 0)
        {
            return;
        }

        RemoveActionsAtIndices(new[] { index }, "Editor_StatusRemovedAction", PostRemoveSelectionPolicy.ClearSelection);
    }

    public void RemoveSelectedActions()
    {
        var indices = SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        if (indices.Length == 0)
        {
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusRemovedSelectedActions", PostRemoveSelectionPolicy.ClearSelection);
    }

    public void DeleteHiddenEvents()
    {
        var indices = Actions
            .Select((action, index) => new { action, index })
            .Where(item => IsHiddenByActiveFilters(item.action, IsInsideMouseDrag(item.index)))
            .Select(item => item.index)
            .ToArray();
        if (indices.Length == 0)
        {
            Status = Localize("Editor_StatusNoHiddenEventsToDelete");
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusDeletedHiddenEvents", PostRemoveSelectionPolicy.PreserveSurvivingSelection);
    }

    public void MoveUp()
    {
        if (SelectedAction == null)
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
        if (SelectedAction == null)
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
        if (indices.Length == 0)
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
        var selectedActions = indices.Select(index => Actions[index]).ToArray();
        foreach (var action in selectedActions)
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
        if (indices.Length == 0)
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
        var selectedActions = indices.Select(index => Actions[index]).Reverse().ToArray();
        foreach (var action in selectedActions)
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
        if (SelectedAction == null)
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
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void DuplicateSelectedActions()
    {
        var indices = GetNormalizedSelectedActionIndices();
        if (indices.Length == 0)
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
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void ClearAll()
    {
        if (Actions.Count == 0)
        {
            return;
        }

        SaveUndoState();
        Actions.Clear();
        SetLoadWarnings(Array.Empty<EditorActionRestoreWarning>());
        SelectedAction = null;
        Status = Localize("Editor_StatusClearedAllActions");
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    private void RemoveActionsAtIndices(
        IReadOnlyCollection<int> indices,
        string statusKey,
        PostRemoveSelectionPolicy postRemoveSelectionPolicy)
    {
        var orderedIndices = NormalizeActionIndices(indices);
        if (orderedIndices.Length == 0)
        {
            return;
        }

        var selectedActionsBeforeRemoval = postRemoveSelectionPolicy == PostRemoveSelectionPolicy.PreserveSurvivingSelection
            ? GetSelectedActions()
            : Array.Empty<EditorAction>();

        if (!CanApplyScriptStructureMutation(candidate => RemoveIndicesDescending(candidate, orderedIndices)))
        {
            return;
        }

        SaveUndoState();
        RemoveIndicesDescending(Actions, orderedIndices);
        ApplyPostRemoveSelection(selectedActionsBeforeRemoval, postRemoveSelectionPolicy);
        Status = Localize(statusKey);
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    private int[] GetNormalizedSelectedActionIndices()
    {
        return NormalizeActionIndices(SelectedActionUnderlyingIndices);
    }

    private int[] NormalizeActionIndices(IEnumerable<int> indices)
    {
        return indices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
    }

    private void SetSelectedActionUnderlyingIndices(IEnumerable<int> indices)
    {
        _isSynchronizingSelectedUnderlyingIndices = true;
        try
        {
            SelectedActionUnderlyingIndices.Clear();
            foreach (var index in indices)
            {
                SelectedActionUnderlyingIndices.Add(index);
            }
        }
        finally
        {
            _isSynchronizingSelectedUnderlyingIndices = false;
        }

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
                throw new ArgumentOutOfRangeException(nameof(postRemoveSelectionPolicy), postRemoveSelectionPolicy, null);
        }
    }

    private void PreserveSurvivingSelection(IReadOnlyCollection<EditorAction> selectedActionsBeforeRemoval)
    {
        if (selectedActionsBeforeRemoval.Count == 0)
        {
            ClearActionSelection();
            return;
        }

        var survivingSelectedIndices = selectedActionsBeforeRemoval
            .Select(action => Actions.IndexOf(action))
            .Where(index => index >= 0)
            .ToArray();

        if (survivingSelectedIndices.Length == 0)
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
        SetSelectedActionUnderlyingIndices(Array.Empty<int>());
        SyncSelectedActionListItem();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        ResetPropertyEditUndoCoalescing();
        _isRestoringState = true;
        try
        {
            var currentState = CloneState(Actions);
            _redoStack.Push(currentState);

            var previousState = _undoStack.Pop();
            Actions.Clear();
            foreach (var action in previousState)
            {
                Actions.Add(action);
            }

            SelectedAction = Actions.FirstOrDefault();
            Status = Localize("Editor_StatusUndone");

            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
            RememberCurrentState();
        }
        finally
        {
            _isRestoringState = false;
        }
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        ResetPropertyEditUndoCoalescing();
        _isRestoringState = true;
        try
        {
            var currentState = CloneState(Actions);
            _undoStack.Push(currentState);

            var nextState = _redoStack.Pop();
            Actions.Clear();
            foreach (var action in nextState)
            {
                Actions.Add(action);
            }

            SelectedAction = Actions.FirstOrDefault();
            Status = Localize("Editor_StatusRedone");

            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
            RememberCurrentState();
        }
        finally
        {
            _isRestoringState = false;
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
            else if (actionType == EditorActionType.BlockEnd)
            {
                depth--;
                if (depth == 0)
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
        if (endIndex < 0 || endIndex >= Actions.Count || Actions[endIndex].Type != EditorActionType.BlockEnd)
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

            if (actionType != EditorActionType.BlockEnd)
            {
                continue;
            }

            if (blockStack.Count == 0)
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
        if (SelectedAction == null)
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

        if (SelectedAction == null)
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

        if (SelectedAction.Type != EditorActionType.BlockEnd)
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
            || Actions[startIndex].Type != EditorActionType.IfBlockStart)
        {
            return;
        }

        var elseIndex = endIndex + 1;
        if (elseIndex >= Actions.Count || Actions[elseIndex].Type != EditorActionType.ElseBlockStart)
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
        var candidate = CloneState(Actions);
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
        foreach (var index in indices.OrderBy(index => index))
        {
            MoveAction(actions, index, index - 1);
        }
    }

    private static void MoveIndicesDown(List<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.OrderByDescending(index => index))
        {
            MoveAction(actions, index, index + 1);
        }
    }

    private static void InsertClones(IList<EditorAction> actions, IReadOnlyList<int> indices, int insertionIndex)
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
        foreach (var index in indices.OrderByDescending(index => index))
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
