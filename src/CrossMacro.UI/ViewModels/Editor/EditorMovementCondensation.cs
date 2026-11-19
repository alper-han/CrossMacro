namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Previews and applies the removal of redundant mouse-move runs below a time threshold.</summary>
public sealed class EditorMovementCondensation : ObservableObject
{
    private readonly EditorViewModel _owner;

    internal EditorMovementCondensation(EditorViewModel owner)
    {
        _owner = owner;
    }

    public int ThresholdMilliseconds
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _owner.UpdateActionListPresentation();
            }
        }
    } = 10;

    public bool CanApply => Plan.RemovedActionIndices.Count > 0;

    public string PreviewSummary
    {
        get
        {
            var plan = Plan;
            if (plan.Runs.Count is 0)
            {
                return _owner.Localize("Editor_MovementCondensationNoPreview");
            }

            var removedMoveCount = 0;
            var removedDelayCount = 0;
            var removedDelayMicroseconds = 0L;
            foreach (var run in plan.Runs)
            {
                removedMoveCount += run.RemovedMoveCount;
                removedDelayCount += run.RemovedDelayCount;
                removedDelayMicroseconds = removedDelayMicroseconds > long.MaxValue - run.RemovedDelayMicroseconds
                    ? long.MaxValue
                    : removedDelayMicroseconds + run.RemovedDelayMicroseconds;
            }

            var milliseconds = ((decimal)removedDelayMicroseconds / 1_000m)
                .ToString("0.###", CultureInfo.CurrentCulture);
            return string.Format(
                CultureInfo.CurrentCulture,
                _owner.Localize("Editor_MovementCondensationPreviewSummary"),
                removedMoveCount,
                removedDelayCount,
                milliseconds);
        }
    }

    public void Apply()
    {
        var plan = Plan;
        if (plan.RemovedActionIndices.Count is 0)
        {
            return;
        }

        _owner.RemoveCondensedMovement(plan.RemovedActionIndices);
    }

    internal MouseMovementCondensationPlan Plan { get => field ??= Create(); private set; }

    internal bool HasCondensibleRun => Plan.Runs.Count > 0;

    internal void RefreshPlan() => Plan = Create();

    internal void NotifyChanged()
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(PreviewSummary));
    }

    private MouseMovementCondensationPlan Create() =>
        MouseMovementCondensationPlan.Create(_owner.Actions, (long)ThresholdMilliseconds * 1_000);
}
