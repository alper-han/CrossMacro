namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// The structural edits of the action list: adding, inserting, removing, moving, duplicating and clearing
/// actions. Every edit is first checked against block structure on a copy, then recorded for undo.
/// </summary>
internal sealed class EditorActionListEditor(EditorViewModel owner, EditorActionDisplayFormatter actionDisplayFormatter)
{
    private enum PostRemoveSelectionPolicy
    {
        ClearSelection,
        PreserveSurvivingSelection,
    }

    private ObservableCollection<EditorAction> Actions => owner.Actions;

    internal void AddAction()
    {
        var newType = owner.Picker.ActionType;
        if (!EditorViewModel.IsUserAddableActionType(newType))
        {
            owner.Status = owner.Localize("Editor_StatusAutoManagedAction");
            return;
        }

        var insertionIndex = GetInsertionIndexAfterSelection();
        var autoManagedBlock = EditorActionListStructure.IsAutoManagedBlockStart(newType);
        if (!CanApplyStructureMutation(candidate =>
            {
                candidate.Insert(insertionIndex, new EditorAction { Type = newType });
                if (autoManagedBlock)
                {
                    candidate.Insert(insertionIndex + 1, new EditorAction { Type = EditorActionType.BlockEnd });
                }
            }))
        {
            return;
        }

        owner.SaveUndoState();

        var isAbsolute = !EditorActionCoordinateRules.UsesCoordinates(newType) || GetCurrentCoordinateMode(insertionIndex);
        var action = EditorNewActionFactory.Create(newType, isAbsolute, owner.ImageAssetNames);
        Actions.Insert(insertionIndex, action);
        if (autoManagedBlock)
        {
            Actions.Insert(insertionIndex + 1, new EditorAction { Type = EditorActionType.BlockEnd });
        }

        owner.SelectedAction = action;
        owner.Status = owner.LocalizeFormat("Editor_StatusAddedAction", actionDisplayFormatter.Format(action));
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void AddCurrentPositionClick()
    {
        var insertionIndex = GetInsertionIndexAfterSelection();
        if (!CanApplyStructureMutation(candidate =>
            candidate.Insert(insertionIndex, new EditorAction { Type = EditorActionType.MouseClick })))
        {
            return;
        }

        owner.SaveUndoState();

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
        owner.SkipInitialZeroZero = true;
        owner.SelectedAction = action;
        owner.Status = owner.LocalizeFormat("Editor_StatusAddedAction", actionDisplayFormatter.Format(action));
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void InsertElseBlock()
    {
        var ifStart = owner.SelectedAction;
        if (ifStart?.Type is not EditorActionType.IfBlockStart)
        {
            owner.Status = owner.Localize("Editor_StatusSelectIfBlockFirst");
            return;
        }

        var ifStartIndex = Actions.IndexOf(ifStart);
        if (ifStartIndex < 0 || !EditorActionListStructure.TryFindMatchingBlockEnd(Actions, ifStartIndex, out var ifBlockEndIndex))
        {
            owner.Status = owner.Localize("Editor_StatusSelectIfBlockFirst");
            return;
        }

        if (ifBlockEndIndex + 1 < Actions.Count && Actions[ifBlockEndIndex + 1].Type is EditorActionType.ElseBlockStart)
        {
            owner.Status = owner.Localize("Editor_StatusElseAlreadyExists");
            return;
        }

        owner.SaveUndoState();

        var elseStartAction = new EditorAction { Type = EditorActionType.ElseBlockStart };
        Actions.Insert(ifBlockEndIndex + 1, elseStartAction);
        Actions.Insert(ifBlockEndIndex + 2, new EditorAction { Type = EditorActionType.BlockEnd });
        owner.SelectedAction = elseStartAction;

        owner.Status = owner.Localize("Editor_StatusInsertedElseBlock");
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void RemoveBlock()
    {
        if (!EditorActionListStructure.TryGetBlockRange(Actions, owner.SelectedAction, out var startIndex, out var endIndex))
        {
            owner.Status = owner.Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (!CanApplyStructureMutation(candidate =>
            {
                for (var index = endIndex; index >= startIndex; index--)
                {
                    candidate.RemoveAt(index);
                }
            }))
        {
            return;
        }

        owner.SaveUndoState();
        for (var index = endIndex; index >= startIndex; index--)
        {
            Actions.RemoveAt(index);
        }

        owner.SelectedAction = Actions.Count > 0
            ? Actions[Math.Min(startIndex, Actions.Count - 1)]
            : null;

        owner.Status = owner.Localize("Editor_StatusRemovedBlock");
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void RemoveAction()
    {
        if (owner.SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(owner.SelectedAction);
        if (index < 0)
        {
            return;
        }

        RemoveActionsAtIndices([index], "Editor_StatusRemovedAction", PostRemoveSelectionPolicy.ClearSelection);
    }

    internal void RemoveSelectedActions()
    {
        var indices = owner.GetSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusRemovedSelectedActions", PostRemoveSelectionPolicy.ClearSelection);
    }

    internal void DeleteHiddenEvents()
    {
        var indices = new List<int>();
        var buttonAnalysis = owner.Movement.Plan.ButtonAnalysis;
        for (var index = 0; index < Actions.Count; index++)
        {
            if (EditorActionListMetadata.IsHidden(Actions[index], owner.Filters.HideMouseMoves, owner.Filters.HideShortWaits, buttonAnalysis.IsDefinitelyIdle(index)))
            {
                indices.Add(index);
            }
        }

        if (indices.Count is 0)
        {
            owner.Status = owner.Localize("Editor_StatusNoHiddenEventsToDelete");
            return;
        }

        RemoveActionsAtIndices(indices, "Editor_StatusDeletedHiddenEvents", PostRemoveSelectionPolicy.PreserveSurvivingSelection);
    }

    internal void RemoveCondensedMovement(IReadOnlyCollection<int> indices) =>
        RemoveActionsAtIndices(indices, "Editor_StatusCondensedMovement", PostRemoveSelectionPolicy.PreserveSurvivingSelection);

    internal void MoveUp() => MoveSelectedAction(offset: -1, "Editor_StatusMovedActionUp");

    internal void MoveDown() => MoveSelectedAction(offset: 1, "Editor_StatusMovedActionDown");

    internal void MoveSelectedActionsUp()
    {
        var indices = owner.GetSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        if (indices[0] <= 0)
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (!CanApplyStructureMutation(candidate => EditorActionListStructure.MoveIndicesUp(candidate, indices)))
        {
            return;
        }

        owner.SaveUndoState();
        foreach (var action in indices.Select(index => Actions[index]).ToArray())
        {
            var currentIndex = Actions.IndexOf(action);
            Actions.Move(currentIndex, currentIndex - 1);
        }

        owner.SelectUnderlyingIndices(indices.Select(index => index - 1));
        owner.Status = owner.Localize("Editor_StatusMovedSelectedActionsUp");
        owner.FinishListEdit(actionCountChanged: false);
    }

    internal void MoveSelectedActionsDown()
    {
        var indices = owner.GetSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        if (indices[^1] >= Actions.Count - 1)
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (!CanApplyStructureMutation(candidate => EditorActionListStructure.MoveIndicesDown(candidate, indices)))
        {
            return;
        }

        owner.SaveUndoState();
        foreach (var action in indices.Select(index => Actions[index]).Reverse().ToArray())
        {
            var currentIndex = Actions.IndexOf(action);
            Actions.Move(currentIndex, currentIndex + 1);
        }

        owner.SelectUnderlyingIndices(indices.Select(index => index + 1));
        owner.Status = owner.Localize("Editor_StatusMovedSelectedActionsDown");
        owner.FinishListEdit(actionCountChanged: false);
    }

    internal void DuplicateAction()
    {
        var selected = owner.SelectedAction;
        if (selected is null)
        {
            return;
        }

        var index = Actions.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        if (!CanApplyStructureMutation(candidate => candidate.Insert(index + 1, candidate[index].Clone())))
        {
            return;
        }

        owner.SaveUndoState();
        var clone = selected.Clone();
        Actions.Insert(index + 1, clone);
        owner.SelectedAction = clone;
        owner.Status = owner.Localize("Editor_StatusDuplicatedAction");
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void DuplicateSelectedActions()
    {
        var indices = owner.GetSelectedActionIndices();
        if (indices.Length is 0)
        {
            return;
        }

        var insertionIndex = indices[^1] + 1;
        if (!CanApplyStructureMutation(candidate => EditorActionListStructure.InsertClones(candidate, indices, insertionIndex)))
        {
            return;
        }

        owner.SaveUndoState();
        var clones = indices.Select(index => Actions[index].Clone()).ToArray();
        for (var offset = 0; offset < clones.Length; offset++)
        {
            Actions.Insert(insertionIndex + offset, clones[offset]);
        }

        owner.SelectUnderlyingIndices(Enumerable.Range(insertionIndex, clones.Length));
        owner.Status = owner.Localize("Editor_StatusDuplicatedSelectedActions");
        owner.FinishListEdit(actionCountChanged: true);
    }

    internal void ClearAll()
    {
        if (Actions.Count is 0)
        {
            return;
        }

        owner.SaveUndoState();
        Actions.Clear();
        owner.SetLoadWarnings([]);
        owner.SelectedAction = null;
        owner.Status = owner.Localize("Editor_StatusClearedAllActions");
        owner.FinishListEdit(actionCountChanged: true);
    }

    private void MoveSelectedAction(int offset, string statusKey)
    {
        var selected = owner.SelectedAction;
        if (selected is null)
        {
            return;
        }

        var index = Actions.IndexOf(selected);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= Actions.Count)
        {
            return;
        }

        if (!CanApplyStructureMutation(candidate => EditorActionListStructure.MoveAction(candidate, index, destination)))
        {
            return;
        }

        owner.SaveUndoState();
        Actions.Move(index, destination);
        owner.SelectedAction = selected;
        owner.Status = owner.Localize(statusKey);
        owner.FinishListEdit(actionCountChanged: false);
    }

    private void RemoveActionsAtIndices(
        IReadOnlyCollection<int> indices,
        string statusKey,
        PostRemoveSelectionPolicy postRemoveSelectionPolicy)
    {
        var orderedIndices = owner.NormalizeActionIndices(indices);
        if (orderedIndices.Length is 0)
        {
            return;
        }

        var selectedActionsBeforeRemoval = postRemoveSelectionPolicy is PostRemoveSelectionPolicy.PreserveSurvivingSelection
            ? owner.GetSelectedActions()
            : [];

        if (!CanApplyStructureMutation(candidate => EditorActionListStructure.RemoveIndicesDescending(candidate, orderedIndices)))
        {
            return;
        }

        owner.SaveUndoState();
        owner.IsBatchUpdatingActions = true;
        try
        {
            EditorActionListStructure.RemoveIndicesDescending(Actions, orderedIndices);
        }
        finally
        {
            owner.IsBatchUpdatingActions = false;
        }

        owner.RefreshActionCollectionState();
        ApplyPostRemoveSelection(selectedActionsBeforeRemoval, postRemoveSelectionPolicy);
        owner.Status = owner.Localize(statusKey);
        owner.FinishListEdit(actionCountChanged: true);
    }

    private void ApplyPostRemoveSelection(
        IReadOnlyCollection<EditorAction> selectedActionsBeforeRemoval,
        PostRemoveSelectionPolicy postRemoveSelectionPolicy)
    {
        switch (postRemoveSelectionPolicy)
        {
            case PostRemoveSelectionPolicy.ClearSelection:
                owner.ClearActionSelection();
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
        var survivingSelectedIndices = selectedActionsBeforeRemoval
            .Select(action => Actions.IndexOf(action))
            .Where(index => index >= 0)
            .ToArray();

        if (survivingSelectedIndices.Length is 0)
        {
            owner.ClearActionSelection();
            return;
        }

        owner.SelectUnderlyingIndices(survivingSelectedIndices);
    }

    private int GetInsertionIndexAfterSelection()
    {
        if (owner.SelectedAction is null)
        {
            return Actions.Count;
        }

        var selectedIndex = Actions.IndexOf(owner.SelectedAction);
        return selectedIndex >= 0 ? selectedIndex + 1 : Actions.Count;
    }

    /// <summary>The coordinate mode a newly inserted coordinate action should inherit from its surroundings.</summary>
    private bool GetCurrentCoordinateMode(int insertionIndex)
    {
        if (TryGetCoordinateMode(owner.SelectedAction, out var selectedMode))
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

        return Actions.FirstOrDefault(EditorActionCoordinateRules.HasPositionCoordinates)?.IsAbsolute ?? true;
    }

    private static bool TryGetCoordinateMode(EditorAction? action, out bool isAbsolute)
    {
        if (action is not null && EditorActionCoordinateRules.HasPositionCoordinates(action))
        {
            isAbsolute = action.IsAbsolute;
            return true;
        }

        isAbsolute = true;
        return false;
    }

    private bool CanApplyStructureMutation(Action<List<EditorAction>> mutation)
    {
        var candidate = EditorViewModel.CloneActions(Actions);
        mutation(candidate);
        if (ScriptBlockStructureValidator.Validate(candidate).IsValid)
        {
            return true;
        }

        owner.Status = owner.Localize("Editor_StatusOperationBlocked");
        return false;
    }
}
