namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// UI editing buffer for a persisted Core task. It records whether the user changed the buffer
/// since it was last loaded, so a refresh never overwrites an unsaved draft.
/// </summary>
public abstract partial class TaskEditorBase<TTask>(string defaultName) : ObservableObject
    where TTask : class
{
    private bool _isApplyingPersistedState;

    [ObservableProperty] private Guid id;
    [ObservableProperty] private string name = defaultName;

    internal long ScopeGeneration { get; init; }

    /// <summary>True when the user edited the buffer after the last <see cref="Load"/>.</summary>
    internal bool IsDirty { get; private set; }

    public void Load(TTask source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ApplyPersistedState(() => LoadCore(source));
        IsDirty = false;
    }

    public abstract TTask ToCore();

    protected abstract void LoadCore(TTask source);

    /// <summary>
    /// Runtime state and derived values are not user edits; everything else raised by the buffer is.
    /// </summary>
    protected abstract bool IsConfigurationProperty(string? propertyName);

    /// <summary>Records a user edit that does not surface as a property change, such as a collection edit.</summary>
    protected void MarkEdited()
    {
        if (!_isApplyingPersistedState)
        {
            IsDirty = true;
        }
    }

    /// <summary>Runs <paramref name="apply"/> without recording its changes as user edits.</summary>
    private protected void ApplyPersistedState(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var wasApplying = _isApplyingPersistedState;
        _isApplyingPersistedState = true;
        try
        {
            apply();
        }
        finally
        {
            _isApplyingPersistedState = wasApplying;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPropertyChanged(e);
        if (IsConfigurationProperty(e.PropertyName))
        {
            MarkEdited();
        }
    }
}
