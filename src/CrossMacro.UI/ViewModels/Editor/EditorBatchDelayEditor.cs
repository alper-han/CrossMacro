namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Edits the timing of several selected delay actions at once.</summary>
public sealed class EditorBatchDelayEditor : ObservableObject
{
    private readonly EditorViewModel _owner;

    internal EditorBatchDelayEditor(EditorViewModel owner)
    {
        _owner = owner;
    }

    public bool IsAvailable => _owner.SelectedActionCount > 1 && _owner.GetSelectedActions().All(action => action.Type is EditorActionType.Delay);

    public bool ShowFixedInput => IsAvailable && !UseRandomDelay;

    public bool ShowRandomOptions => IsAvailable && UseRandomDelay;

    public bool UseRandomDelay
    {
        get => FirstDelay()?.UseRandomDelay ?? false;
        set => Apply(
            nameof(EditorAction.UseRandomDelay),
            action => action.UseRandomDelay != value,
            action => action.UseRandomDelay = value);
    }

    public int DelayMs
    {
        get => FirstDelay()?.DelayMs ?? 0;
        set => Apply(
            nameof(EditorAction.DelayMs),
            action => action.DelayMs != value,
            action => action.DelayMs = value);
    }

    public string Duration
    {
        get => MacroTiming.FormatDuration(FirstDelay()?.DelayMicroseconds ?? 0);
        set
        {
            if (!MacroTiming.TryParseDurationMicroseconds(value, out var microseconds))
            {
                return;
            }

            Apply(
                nameof(EditorAction.DelayMicroseconds),
                action => action.DelayMicroseconds != microseconds,
                action => action.DelayMicroseconds = microseconds);
        }
    }

    public int RandomMinMs
    {
        get => FirstDelay()?.RandomDelayMinMs ?? 0;
        set => Apply(
            nameof(EditorAction.RandomDelayMinMs),
            action => action.RandomDelayMinMs != value,
            action => action.RandomDelayMinMs = value);
    }

    public int RandomMaxMs
    {
        get => FirstDelay()?.RandomDelayMaxMs ?? 0;
        set => Apply(
            nameof(EditorAction.RandomDelayMaxMs),
            action => action.RandomDelayMaxMs != value,
            action => action.RandomDelayMaxMs = value);
    }

    internal void NotifyChanged() => OnPropertyChanged(string.Empty);

    private EditorAction? FirstDelay() => SelectedDelays().FirstOrDefault();

    private EditorAction[] SelectedDelays() =>
        _owner.GetSelectedActions().Where(action => action.Type is EditorActionType.Delay).ToArray();

    private void Apply(string propertyName, Func<EditorAction, bool> shouldUpdate, Action<EditorAction> update)
    {
        var actions = SelectedDelays();
        if (actions.Length is 0 || !actions.Any(shouldUpdate))
        {
            return;
        }

        _owner.SaveUndoState();
        _owner.IsSynchronizingActionProperties = true;
        try
        {
            foreach (var action in actions)
            {
                update(action);
            }
        }
        finally
        {
            _owner.IsSynchronizingActionProperties = false;
        }

        _owner.UpdateActionListPresentation();
        _owner.NotifyVisibilityChanged();
        _owner.NotifySelectedActionsChanged();
        _owner.ResetPropertyEditUndoCoalescing();
        _owner.RememberCurrentState();

        OnPropertyChanged(propertyName switch
        {
            nameof(EditorAction.UseRandomDelay) => nameof(UseRandomDelay),
            nameof(EditorAction.DelayMs) or nameof(EditorAction.DelayMicroseconds) => nameof(Duration),
            nameof(EditorAction.RandomDelayMinMs) => nameof(RandomMinMs),
            nameof(EditorAction.RandomDelayMaxMs) => nameof(RandomMaxMs),
            _ => string.Empty,
        });
    }
}
