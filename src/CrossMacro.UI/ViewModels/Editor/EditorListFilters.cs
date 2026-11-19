namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Toggles that hide low-importance rows from the action list, and the state the toggles depend on.</summary>
public sealed class EditorListFilters : ObservableObject
{
    private readonly EditorViewModel _owner;

    internal EditorListFilters(EditorViewModel owner)
    {
        _owner = owner;
    }

    // Kept manual: the list must be rebuilt after the change notifications; a generated OnChanged hook would run before them.
    public bool HideMouseMoves
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideMouseMovesToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            _owner.UpdateActionListPresentation();
        }
    }

    // Kept manual: the list must be rebuilt after the change notifications; a generated OnChanged hook would run before them.
    public bool HideShortWaits
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideShortWaitsToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            _owner.UpdateActionListPresentation();
        }
    }

    // Kept manual: the list must be rebuilt after the change notifications; a generated OnChanged hook would run before them.
    public bool SimplifyMovement
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSimplifyMovementToggle));
            _owner.UpdateActionListPresentation();
        }
    }

    public int HiddenEventCount
    {
        get;
        internal set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasHiddenEvents));
            }
        }
    }

    public bool HasHiddenEvents => HiddenEventCount > 0;

    public bool CanDeleteHiddenEvents => EditorActionListMetadata.HasHiddenActions(_owner.Actions, HideMouseMoves, HideShortWaits, _owner.Movement.Plan.ButtonAnalysis);

    public bool ShowDeleteHiddenEvents => (HideMouseMoves || HideShortWaits) && CanDeleteHiddenEvents;

    public bool CanHideMouseMoves => _owner.Actions.Any(action => action.Type is EditorActionType.MouseMove);

    public bool ShowHideMouseMovesToggle => HideMouseMoves || CanHideMouseMoves;

    public bool CanHideShortWaits => _owner.Actions.Any(EditorActionListMetadata.IsShortWait);

    public bool ShowHideShortWaitsToggle => HideShortWaits || CanHideShortWaits;

    public bool CanSimplifyMovement => _owner.Movement.HasCondensibleRun;

    public bool ShowSimplifyMovementToggle => SimplifyMovement || CanSimplifyMovement;

    internal void NotifyChanged() => OnPropertyChanged(string.Empty);
}
