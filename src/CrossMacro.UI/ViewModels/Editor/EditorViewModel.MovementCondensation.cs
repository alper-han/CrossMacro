namespace CrossMacro.UI.ViewModels.Editor;

public partial class EditorViewModel
{
    private MouseMovementCondensationPlan? _movementCondensationPlan;

    public bool ShowMovementCleanupControls => Actions.Count > 0;

    [ObservableProperty]
    public partial int MovementCondensationThresholdMilliseconds { get; set; } = 10;

    partial void OnMovementCondensationThresholdMillisecondsChanged(int value)
    {
        UpdateActionListPresentation();
    }

    public bool CanApplyMovementCondensation => GetMovementCondensationPlan().RemovedActionIndices.Count > 0;

    public string MovementCondensationPreviewSummary
    {
        get
        {
            var plan = GetMovementCondensationPlan();
            if (plan.Runs.Count is 0)
            {
                return Localize("Editor_MovementCondensationNoPreview");
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
                Localize("Editor_MovementCondensationPreviewSummary"),
                removedMoveCount,
                removedDelayCount,
                milliseconds);
        }
    }

    public void ApplyMovementCondensation()
    {
        var plan = GetMovementCondensationPlan();
        if (plan.RemovedActionIndices.Count is 0)
        {
            return;
        }

        RemoveActionsAtIndices(
            plan.RemovedActionIndices,
            "Editor_StatusCondensedMovement",
            PostRemoveSelectionPolicy.PreserveSurvivingSelection);
    }

    private MouseMovementCondensationPlan GetMovementCondensationPlan()
    {
        return _movementCondensationPlan ??= CreateMovementCondensationPlan();
    }

    private MouseMovementCondensationPlan CreateMovementCondensationPlan()
    {
        return MouseMovementCondensationPlan.Create(
            Actions,
            (long)MovementCondensationThresholdMilliseconds * 1_000);
    }

    private void RefreshMovementCondensationPlan()
    {
        _movementCondensationPlan = CreateMovementCondensationPlan();
    }
}
