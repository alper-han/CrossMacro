namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Block-structure queries and index-based mutations over a flat editor action list.</summary>
internal static class EditorActionListStructure
{
    internal static bool IsScriptBlockStart(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.ElseBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    internal static bool IsAutoManagedBlockStart(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    internal static bool TryFindMatchingBlockEnd(IList<EditorAction> actions, int startIndex, out int endIndex)
    {
        endIndex = -1;
        if (startIndex < 0 || startIndex >= actions.Count || !IsScriptBlockStart(actions[startIndex].Type))
        {
            return false;
        }

        var depth = 0;
        for (var index = startIndex; index < actions.Count; index++)
        {
            var actionType = actions[index].Type;
            if (IsScriptBlockStart(actionType))
            {
                depth++;
            }
            else if (actionType is EditorActionType.BlockEnd)
            {
                depth--;
                if (depth is 0)
                {
                    endIndex = index;
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool TryFindMatchingBlockStart(IList<EditorAction> actions, int endIndex, out int startIndex)
    {
        startIndex = -1;
        if (endIndex < 0 || endIndex >= actions.Count || actions[endIndex].Type is not EditorActionType.BlockEnd)
        {
            return false;
        }

        var blockStack = new Stack<int>();
        for (var index = 0; index <= endIndex; index++)
        {
            var actionType = actions[index].Type;
            if (IsScriptBlockStart(actionType))
            {
                blockStack.Push(index);
                continue;
            }

            if (actionType is not EditorActionType.BlockEnd)
            {
                continue;
            }

            if (blockStack.Count is 0)
            {
                return false;
            }

            var matchedStartIndex = blockStack.Pop();
            if (index == endIndex)
            {
                startIndex = matchedStartIndex;
                return true;
            }
        }

        return false;
    }

    /// <summary>The block the selected start or end action belongs to; an if block's range includes its else block.</summary>
    internal static bool TryGetBlockRange(IList<EditorAction> actions, EditorAction? selected, out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;

        if (selected is null)
        {
            return false;
        }

        var selectedIndex = actions.IndexOf(selected);
        if (selectedIndex < 0)
        {
            return false;
        }

        if (IsScriptBlockStart(selected.Type))
        {
            startIndex = selectedIndex;
            if (!TryFindMatchingBlockEnd(actions, selectedIndex, out endIndex))
            {
                return false;
            }
        }
        else if (selected.Type is EditorActionType.BlockEnd)
        {
            endIndex = selectedIndex;
            if (!TryFindMatchingBlockStart(actions, selectedIndex, out startIndex))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        ExtendIfRangeWithElse(actions, startIndex, ref endIndex);
        return true;
    }

    internal static void MoveAction(List<EditorAction> actions, int sourceIndex, int destinationIndex)
    {
        var action = actions[sourceIndex];
        actions.RemoveAt(sourceIndex);
        actions.Insert(destinationIndex, action);
    }

    internal static void MoveIndicesUp(List<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.Order())
        {
            MoveAction(actions, index, index - 1);
        }
    }

    internal static void MoveIndicesDown(List<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.OrderDescending())
        {
            MoveAction(actions, index, index + 1);
        }
    }

    internal static void InsertClones(List<EditorAction> actions, IReadOnlyList<int> indices, int insertionIndex)
    {
        var clones = indices
            .Select(index => actions[index].Clone())
            .ToArray();
        for (var offset = 0; offset < clones.Length; offset++)
        {
            actions.Insert(insertionIndex + offset, clones[offset]);
        }
    }

    internal static void RemoveIndicesDescending(IList<EditorAction> actions, IEnumerable<int> indices)
    {
        foreach (var index in indices.OrderDescending())
        {
            actions.RemoveAt(index);
        }
    }

    private static void ExtendIfRangeWithElse(IList<EditorAction> actions, int startIndex, ref int endIndex)
    {
        if (startIndex < 0
            || startIndex >= actions.Count
            || actions[startIndex].Type is not EditorActionType.IfBlockStart)
        {
            return;
        }

        var elseIndex = endIndex + 1;
        if (elseIndex >= actions.Count || actions[elseIndex].Type is not EditorActionType.ElseBlockStart)
        {
            return;
        }

        if (TryFindMatchingBlockEnd(actions, elseIndex, out var elseEndIndex))
        {
            endIndex = elseEndIndex;
        }
    }
}
