using System.Collections.Specialized;

namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Subscribes to every action in the document so edits to any row mark the document dirty and refresh the list,
/// and keeps the subscriptions in step with the action collection.
/// </summary>
internal sealed class EditorActionSubscriptions(EditorViewModel owner, EditorSelectedActionReactions reactions)
{
    private readonly HashSet<EditorAction> _subscribed = [];

    internal void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset)
        {
            ResubscribeAll();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EditorAction>().Where(item => _subscribed.Remove(item)))
            {
                item.PropertyChanged -= OnActionPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EditorAction>().Where(item => _subscribed.Add(item)))
            {
                item.PropertyChanged += OnActionPropertyChanged;
            }
        }
    }

    internal void UnsubscribeAll()
    {
        foreach (var action in _subscribed)
        {
            action.PropertyChanged -= OnActionPropertyChanged;
        }

        _subscribed.Clear();
    }

    private void ResubscribeAll()
    {
        UnsubscribeAll();
        owner.SelectedAction?.PropertyChanged -= reactions.OnPropertyChanged;

        foreach (var action in owner.Actions)
        {
            action.PropertyChanged += OnActionPropertyChanged;
            _ = _subscribed.Add(action);
        }
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        owner.UpdateDirtyState();
        if (!string.Equals(e.PropertyName, nameof(EditorAction.Index), StringComparison.Ordinal))
        {
            owner.UpdateActionListPresentation();
        }

        if (string.Equals(e.PropertyName, nameof(EditorAction.Type), StringComparison.Ordinal))
        {
            owner.NotifyActionTypeChanged();
        }

        if (e.PropertyName is nameof(EditorAction.DelayMs) or nameof(EditorAction.DelayMicroseconds) or nameof(EditorAction.UseRandomDelay))
        {
            owner.NotifyDelayChanged();
        }

        if (EditorPresentationChanges.AffectsVariableNames(e.PropertyName))
        {
            owner.RefreshAvailableVariableNames();
        }

        owner.NotifyScreenReadingChanged();
    }
}
