namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Snapshots the editor document for undo and redo and restores it from them.</summary>
internal sealed class EditorUndoRedo(EditorViewModel owner, EditorHistory history)
{
    internal bool CanUndo => history.CanUndo;

    internal bool CanRedo => history.CanRedo;

    /// <summary>Records the current document as the baseline the next edit is compared against.</summary>
    internal void Remember()
    {
        history.Remember(CloneState());
        owner.UpdateDirtyState();
    }

    /// <summary>Pushes the current document as an undo step.</summary>
    internal void Save() => Save(CloneState());

    /// <summary>Pushes the last remembered baseline as an undo step (used when a property edit begins).</summary>
    internal void SaveBaseline() => Save(history.LastKnownState);

    internal void Clear()
    {
        history.Clear();
        owner.NotifyHistoryChanged();
    }

    internal void ResetCoalescing() => history.ResetCoalescing();

    internal bool ShouldCoalesce(EditorAction? action, string propertyName) => history.ShouldCoalesce(action, propertyName);

    internal void Undo() => Restore(history.CanUndo, history.Undo, "Editor_StatusUndone");

    internal void Redo() => Restore(history.CanRedo, history.Redo, "Editor_StatusRedone");

    private void Save(EditorStateSnapshot state)
    {
        history.Save(state);
        owner.NotifyHistoryChanged();
    }

    private void Restore(bool isAvailable, Func<EditorStateSnapshot, EditorStateSnapshot> step, string statusKey)
    {
        if (!isAvailable)
        {
            return;
        }

        ResetCoalescing();
        owner.IsRestoringState = true;
        try
        {
            RestoreSnapshot(step(CloneState()));

            owner.SelectedAction = owner.Actions.FirstOrDefault();
            owner.Status = owner.Localize(statusKey);

            owner.RefreshActionCollectionState();
            owner.NotifyHistoryChanged();
            owner.NotifyActionCountChanged();
            Remember();
        }
        finally
        {
            owner.IsRestoringState = false;
            owner.UpdateDirtyState();
        }
    }

    private void RestoreSnapshot(EditorStateSnapshot state)
    {
        owner.IsBatchUpdatingActions = true;
        try
        {
            owner.Actions.Clear();
            foreach (var action in state.Actions)
            {
                owner.Actions.Add(action);
            }
        }
        finally
        {
            owner.IsBatchUpdatingActions = false;
        }

        owner.SkipInitialZeroZero = state.SkipInitialZeroZero;
    }

    private EditorStateSnapshot CloneState() => new(EditorViewModel.CloneActions(owner.Actions), owner.SkipInitialZeroZeroFlag);
}
