using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;

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
            && string.Equals(left.Text, right.Text, StringComparison.Ordinal);
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
            .FirstOrDefault(action => UsesCoordinateFields(action.Type) && !IsCurrentPositionClickAction(action))
            ?.IsAbsolute ?? true;
    }

    private void PropagateCoordinateMode(EditorAction sourceAction)
    {
        if (!UsesCoordinateFields(sourceAction.Type))
        {
            return;
        }

        if (IsCurrentPositionClickAction(sourceAction))
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

            if (IsCurrentPositionClickAction(action))
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
            || e.PropertyName == nameof(EditorAction.UseCurrentPosition))
        {
            NotifyVisibilityChanged();
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
        OnPropertyChanged(nameof(ShowMouseButton));
        OnPropertyChanged(nameof(ShowKeyCode));
        OnPropertyChanged(nameof(ShowDelay));
        OnPropertyChanged(nameof(ShowFixedDelayInput));
        OnPropertyChanged(nameof(ShowRandomDelayOptions));
        OnPropertyChanged(nameof(ShowScrollAmount));
        OnPropertyChanged(nameof(ShowTextInput));
        OnPropertyChanged(nameof(SkipInitialZeroZero));
        OnPropertyChanged(nameof(RequiresSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanEditSkipInitialZeroZero));
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

            if (action.Type != EditorActionType.MouseClick && action.UseCurrentPosition)
            {
                action.UseCurrentPosition = false;
            }

            if (action.Type == EditorActionType.MouseClick && action.UseCurrentPosition)
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

        Actions.Add(action);
        SelectedAction = action;
        Status = $"Added {action.DisplayName}";
        OnPropertyChanged(nameof(HasActions));
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    public void AddCurrentPositionClick()
    {
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

        Actions.Add(action);
        PropagateCoordinateMode(action);
        SkipInitialZeroZero = true;
        SelectedAction = action;
        Status = $"Added {action.DisplayName}";
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

        SaveUndoState();
        var index = Actions.IndexOf(SelectedAction);
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

        SaveUndoState();
        var clone = SelectedAction.Clone();
        var index = Actions.IndexOf(SelectedAction);
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
}
