using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
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

    private bool GetCurrentCoordinateMode()
    {
        return Actions
            .FirstOrDefault(action => UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action))
            ?.IsAbsolute ?? true;
    }

    private void PropagateCoordinateMode(EditorAction sourceAction)
    {
        if (!UsesCoordinateFields(sourceAction.Type))
        {
            return;
        }

        if (IsCurrentPositionMouseButtonAction(sourceAction))
        {
            if (sourceAction.IsAbsolute)
            {
                sourceAction.IsAbsolute = false;
            }

            return;
        }

        foreach (var action in Actions)
        {
            if (ReferenceEquals(action, sourceAction) || !UsesCoordinateFields(action.Type))
            {
                continue;
            }

            if (IsCurrentPositionMouseButtonAction(action))
            {
                if (action.IsAbsolute)
                {
                    action.IsAbsolute = false;
                }

                continue;
            }

            action.IsAbsolute = sourceAction.IsAbsolute;
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
            PropagateCoordinateMode(coordAction);
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
                if (action.X != 0)
                {
                    action.X = 0;
                }

                if (action.Y != 0)
                {
                    action.Y = 0;
                }

                if (action.IsAbsolute)
                {
                    action.IsAbsolute = false;
                }
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
            Status = "This action is managed automatically.";
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
        var coordinateMode = isCoordinateAction ? GetCurrentCoordinateMode() : true;

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
        Status = $"Added {action.DisplayName}";
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
        PropagateCoordinateMode(action);
        SkipInitialZeroZero = true;
        SelectedAction = action;
        Status = $"Added {action.DisplayName}";
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void InsertElseBlock()
    {
        if (SelectedAction?.Type != EditorActionType.IfBlockStart)
        {
            Status = StatusSelectIfBlockFirst;
            return;
        }

        var ifStartIndex = Actions.IndexOf(SelectedAction);
        if (ifStartIndex < 0 || !TryFindMatchingBlockEnd(ifStartIndex, out var ifBlockEndIndex))
        {
            Status = StatusSelectIfBlockFirst;
            return;
        }

        if (ifBlockEndIndex + 1 < Actions.Count
            && Actions[ifBlockEndIndex + 1].Type == EditorActionType.ElseBlockStart)
        {
            Status = StatusElseAlreadyExists;
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

        Status = StatusInsertedElseBlock;
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void RemoveBlock()
    {
        if (!TryGetSelectedBlockRange(out var startIndex, out var endIndex))
        {
            Status = StatusSelectActionFirst;
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

        Status = StatusRemovedBlock;
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

        if (!CanApplyScriptStructureMutation(candidate => candidate.RemoveAt(index)))
        {
            return;
        }

        SaveUndoState();
        Actions.Remove(SelectedAction);

        if (Actions.Count > 0)
        {
            SelectedAction = Actions[Math.Min(index, Actions.Count - 1)];
        }
        else
        {
            SelectedAction = null;
        }

        Status = StatusRemovedAction;
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
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
        Status = StatusMovedActionUp;
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
        Status = StatusMovedActionDown;
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
        Status = StatusDuplicatedAction;
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
        Status = StatusClearedAllActions;
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
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
            Status = StatusUndone;

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
            Status = StatusRedone;

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

        Status = StatusOperationBlocked;
        return false;
    }

    private static void MoveAction(List<EditorAction> actions, int sourceIndex, int destinationIndex)
    {
        var action = actions[sourceIndex];
        actions.RemoveAt(sourceIndex);
        actions.Insert(destinationIndex, action);
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
