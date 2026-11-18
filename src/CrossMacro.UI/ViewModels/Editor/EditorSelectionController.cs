namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Keeps the three views of the selection consistent: the primary action, the selected list row, and the
/// set of selected underlying action indices (multi-selection).
/// </summary>
internal sealed class EditorSelectionController(EditorViewModel owner, EditorSelection selection)
{
    internal EditorAction[] GetSelectedActions()
    {
        return selection.UnderlyingIndices
            .Where(index => index >= 0 && index < owner.Actions.Count)
            .Distinct()
            .Order()
            .Select(index => owner.Actions[index])
            .ToArray();
    }

    internal int[] GetSelectedIndices() => Normalize(selection.UnderlyingIndices);

    internal int[] Normalize(IEnumerable<int> indices) => EditorSelection.Normalize(indices, owner.Actions.Count);

    /// <summary>Selects exactly the actions at the given indices and makes the first of them the primary selection.</summary>
    internal void SelectIndices(IEnumerable<int> indices)
    {
        selection.ReplaceIndices(indices);
        owner.NotifySelectedActionsChanged();
        SelectPrimaryFromIndices();
    }

    internal void Clear()
    {
        owner.SelectedAction = null;
        selection.ReplaceIndices([]);
        owner.NotifySelectedActionsChanged();
        SyncSelectedRow();
    }

    /// <summary>Replaces the multi-selection (from the list control) and re-derives the primary selection.</summary>
    internal void ReplaceIndices(IEnumerable<int> underlyingIndices)
    {
        var normalized = Normalize(underlyingIndices);
        if (selection.UnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        WriteIndices(normalized);
        SelectPrimaryFromIndices();
        owner.NotifySelectedActionsChanged();
    }

    /// <summary>Handles an external change of the index collection (for example from the list control).</summary>
    internal void OnIndicesChanged()
    {
        if (selection.IsSynchronizingIndices)
        {
            return;
        }

        NormalizeIndices();
        SelectPrimaryFromIndices();
        owner.NotifySelectedActionsChanged();
    }

    /// <summary>Drops indices that no longer refer to an action.</summary>
    internal void NormalizeIndices()
    {
        if (selection.IsSynchronizingIndices)
        {
            return;
        }

        var normalized = Normalize(selection.UnderlyingIndices);
        if (selection.UnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        WriteIndices(normalized);
    }

    /// <summary>Points the selected list row at the row of the primary action.</summary>
    internal void SyncSelectedRow()
    {
        var primary = owner.SelectedAction;
        var selectedRow = primary is null
            ? null
            : owner.ActionListItems.FirstOrDefault(item => ReferenceEquals(item.Action, primary));
        if (ReferenceEquals(selection.SelectedRow, selectedRow))
        {
            return;
        }

        selection.SelectedRow = selectedRow;
        owner.NotifySelectedActionListItemChanged();
    }

    /// <summary>Makes the selection consist of just the primary action.</summary>
    internal void SyncIndicesToPrimary()
    {
        selection.IsSynchronizingIndices = true;
        try
        {
            selection.UnderlyingIndices.Clear();

            var selectedIndex = owner.SelectedAction is null
                ? -1
                : owner.Actions.IndexOf(owner.SelectedAction);
            if (selectedIndex >= 0)
            {
                selection.UnderlyingIndices.Add(selectedIndex);
            }
        }
        finally
        {
            selection.IsSynchronizingIndices = false;
        }

        owner.NotifySelectedActionsChanged();
    }

    /// <summary>Makes the first selected row's action the primary selection.</summary>
    internal void SelectPrimaryFromIndices()
    {
        var selectedIndexSet = selection.UnderlyingIndices.ToHashSet();
        var selectedRow = owner.ActionListItems
            .Where(item => item.RepresentsSourceAction && selectedIndexSet.Contains(item.UnderlyingIndex))
            .OrderBy(item => item.UnderlyingIndex)
            .FirstOrDefault();

        selection.IsSelectingFromList = true;
        try
        {
            owner.SelectedActionListItem = selectedRow;
            if (!ReferenceEquals(owner.SelectedAction, selectedRow?.Action))
            {
                owner.SelectedAction = selectedRow?.Action;
            }
        }
        finally
        {
            selection.IsSelectingFromList = false;
        }
    }

    private void WriteIndices(int[] normalized)
    {
        selection.IsSynchronizingIndices = true;
        try
        {
            selection.UnderlyingIndices.Clear();
            foreach (var index in normalized)
            {
                selection.UnderlyingIndices.Add(index);
            }
        }
        finally
        {
            selection.IsSynchronizingIndices = false;
        }
    }
}
