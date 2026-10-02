namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class MouseMovementCondensationPlan
{
    private MouseMovementCondensationPlan(
        IReadOnlyList<MouseMovementCondensationRun> runs,
        EditorMouseButtonAnalysis buttonAnalysis,
        IReadOnlyList<int> removedActionIndices)
    {
        Runs = runs;
        ButtonAnalysis = buttonAnalysis;
        RemovedActionIndices = removedActionIndices;
    }

    public EditorMouseButtonAnalysis ButtonAnalysis { get; }

    public IReadOnlyList<MouseMovementCondensationRun> Runs { get; }

    public IReadOnlyList<int> RemovedActionIndices { get; }

    public static MouseMovementCondensationPlan Create(
        IReadOnlyList<EditorAction> actions,
        long thresholdMicroseconds)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var buttonAnalysis = EditorMouseButtonAnalysis.Create(actions);
        if (thresholdMicroseconds <= 0 || actions.Count < 2)
        {
            return new MouseMovementCondensationPlan([], buttonAnalysis, []);
        }

        List<MouseMovementCondensationRun>? runs = null;
        List<int>? removedIndices = null;

        for (var index = 0; index < actions.Count;)
        {
            var action = actions[index];
            if (buttonAnalysis.IsDefinitelyIdle(index) && IsAbsoluteLiteralMove(action))
            {
                var run = TryCreateRun(actions, index, thresholdMicroseconds);
                if (run is not null)
                {
                    runs ??= new List<MouseMovementCondensationRun>();
                    removedIndices ??= new List<int>();
                    runs.Add(run);
                    for (var removedIndex = run.StartIndex; removedIndex < run.FinalMoveIndex; removedIndex++)
                    {
                        removedIndices.Add(removedIndex);
                    }

                    index = run.FinalMoveIndex + 1;
                    continue;
                }
            }

            index++;
        }

        return runs is null
            ? new MouseMovementCondensationPlan([], buttonAnalysis, [])
            : new MouseMovementCondensationPlan(runs, buttonAnalysis, removedIndices!);
    }

    private static MouseMovementCondensationRun? TryCreateRun(
        IReadOnlyList<EditorAction> actions,
        int startIndex,
        long thresholdMicroseconds)
    {
        var finalMoveIndex = startIndex;
        var moveCount = 1;
        var pendingGapMicroseconds = 0L;
        var removedDelayMicroseconds = 0L;

        for (var index = startIndex + 1; index < actions.Count; index++)
        {
            var action = actions[index];
            if (action.Type is EditorActionType.Delay && !action.UseRandomDelay && action.DelayMicroseconds >= 0)
            {
                if (action.DelayMicroseconds >= thresholdMicroseconds - pendingGapMicroseconds)
                {
                    break;
                }

                pendingGapMicroseconds += action.DelayMicroseconds;
                continue;
            }

            if (!IsAbsoluteLiteralMove(action) || pendingGapMicroseconds >= thresholdMicroseconds)
            {
                break;
            }

            finalMoveIndex = index;
            moveCount++;
            removedDelayMicroseconds = SaturatingAdd(removedDelayMicroseconds, pendingGapMicroseconds);
            pendingGapMicroseconds = 0;
        }

        if (moveCount < 2)
        {
            return null;
        }

        return new MouseMovementCondensationRun(
            startIndex,
            finalMoveIndex,
            finalMoveIndex,
            moveCount - 1,
            finalMoveIndex - startIndex - (moveCount - 1),
            removedDelayMicroseconds);
    }

    private static bool IsAbsoluteLiteralMove(EditorAction action)
    {
        return action.Type is EditorActionType.MouseMove
            && action.IsAbsolute
            && !action.HasVariableCoordinates
            && action.TryGetLiteralCoordinates(out _, out _);
    }

    private static long SaturatingAdd(long value, long addition)
    {
        return addition > long.MaxValue - value ? long.MaxValue : value + addition;
    }
}

