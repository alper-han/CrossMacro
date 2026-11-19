namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Builds the rows the action list shows: indentation per block depth, hidden low-importance events, and
/// condensed mouse-move runs collapsed into one representative row.
/// </summary>
internal sealed class EditorActionListBuilder(ILocalizationService localizationService, EditorActionDisplayFormatter actionDisplayFormatter)
{
    internal readonly record struct Result(IReadOnlyList<EditorActionListItem> Items, int HiddenEventCount);

    internal Result Build(
        IReadOnlyList<EditorAction> actions,
        MouseMovementCondensationPlan plan,
        bool hideMouseMoves,
        bool hideShortWaits,
        bool simplifyMovement)
    {
        var items = new List<EditorActionListItem>();
        var blockStack = new Stack<EditorActionType>();
        var buttonAnalysis = plan.ButtonAnalysis;
        var runs = simplifyMovement ? plan.Runs : null;
        var nextRunIndex = 0;
        var depth = 0;
        var hiddenEventCount = 0;

        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            var isInsideDrag = !buttonAnalysis.IsDefinitelyIdle(index);
            if (EditorActionListMetadata.IsHidden(action, hideMouseMoves, hideShortWaits, !isInsideDrag))
            {
                hiddenEventCount++;
                continue;
            }

            var condensedRun = isInsideDrag ? null : TakeRunStartingAt(runs, ref nextRunIndex, index);
            if (condensedRun is not null)
            {
                items.Add(CreateCondensedItem(actions[condensedRun.FinalMoveIndex], condensedRun, depth));
                index = condensedRun.EndIndex;
                continue;
            }

            var isLowImportance = EditorActionListMetadata.IsLowImportance(action, isInsideDrag);
            if (action.Type is EditorActionType.BlockEnd)
            {
                depth = Math.Max(0, depth - 1);
                items.Add(CreateItem(action, index, depth, DescribeBlockEnd(blockStack), isLowImportance, condensedHiddenCount: 0));
                continue;
            }

            items.Add(CreateItem(action, index, depth, actionDisplayFormatter.Format(action), isLowImportance, condensedHiddenCount: 0));
            if (EditorActionListStructure.IsScriptBlockStart(action.Type))
            {
                blockStack.Push(action.Type);
                depth++;
            }
        }

        return new Result(items, hiddenEventCount);
    }

    private static MouseMovementCondensationRun? TakeRunStartingAt(IReadOnlyList<MouseMovementCondensationRun>? runs, ref int nextRunIndex, int index)
    {
        while (runs is not null && nextRunIndex < runs.Count && runs[nextRunIndex].StartIndex < index)
        {
            nextRunIndex++;
        }

        return runs is not null && nextRunIndex < runs.Count && runs[nextRunIndex].StartIndex == index
            ? runs[nextRunIndex++]
            : null;
    }

    private string DescribeBlockEnd(Stack<EditorActionType> blockStack)
    {
        return blockStack.Count > 0
            ? string.Format(
                localizationService.CurrentCulture,
                localizationService["Editor_Action_EndBlock"],
                actionDisplayFormatter.FormatBlockName(blockStack.Pop()))
            : localizationService["Editor_Action_EndBlockShort"];
    }

    private EditorActionListItem CreateCondensedItem(EditorAction representativeAction, MouseMovementCondensationRun run, int depth)
    {
        var isLowImportance = EditorActionListMetadata.IsLowImportance(representativeAction, isInsideDrag: false);
        return CreateItem(
            representativeAction,
            run.FinalMoveIndex,
            depth,
            actionDisplayFormatter.Format(representativeAction),
            isLowImportance,
            run.RemovedMoveCount + run.RemovedDelayCount);
    }

    private EditorActionListItem CreateItem(
        EditorAction action,
        int underlyingIndex,
        int indentLevel,
        string displayName,
        bool isNoise,
        int condensedHiddenCount)
    {
        var condensedHint = condensedHiddenCount > 0
            ? string.Format(
                localizationService.CurrentCulture,
                localizationService["Editor_SimplifiedMovementHint"],
                condensedHiddenCount)
            : string.Empty;

        return new EditorActionListItem(
            action,
            action.Index,
            underlyingIndex,
            indentLevel,
            displayName,
            condensedHint,
            EditorActionListMetadata.GetVisualKind(action, isNoise),
            EditorActionListMetadata.IsImportant(action, isNoise),
            EditorActionListMetadata.IsCleanupEligible(action, isNoise),
            condensedHiddenCount,
            representsSourceAction: true,
            isNoise);
    }
}
