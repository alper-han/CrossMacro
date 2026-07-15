
namespace CrossMacro.Core.Services;

/// <summary>
/// Validates script block structure and loop-control placement for editor actions.
/// </summary>
public static class ScriptBlockStructureValidator
{
    public static ScriptBlockStructureValidationResult Validate(IReadOnlyList<EditorAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var errors = new List<string>();
        var blockStack = new Stack<(EditorActionType Type, int Index)>();
        var blockEndToStart = new Dictionary<int, int>();

        for (var index = 0; index < actions.Count; index++)
        {
            var type = actions[index].Type;
            if (EditorActionScriptClassifier.IsScriptBlockStartAction(type))
            {
                blockStack.Push((type, index));
                continue;
            }

            if (type is not EditorActionType.BlockEnd)
            {
                if (EditorActionScriptClassifier.IsLoopControlAction(type) && !HasEnclosingLoop(blockStack))
                {
                    errors.Add(string.Create(CultureInfo.InvariantCulture, $"Action {index + 1}: {type} can only be used inside repeat/while/for blocks."));
                }

                continue;
            }

            if (blockStack.Count is 0)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"Action {index + 1}: unexpected block end '}}'."));
                continue;
            }

            var start = blockStack.Pop();
            blockEndToStart[index] = start.Index;
        }

        while (blockStack.Count > 0)
        {
            var unclosed = blockStack.Pop();
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"Action {unclosed.Index + 1}: block is not closed with a matching '}}'."));
        }

        ValidateElseBlocks(actions, blockEndToStart, errors);

        return new ScriptBlockStructureValidationResult(errors);
    }

    private static void ValidateElseBlocks(
        IReadOnlyList<EditorAction> actions,
        Dictionary<int, int> blockEndToStart,
        List<string> errors)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (actions[index].Type is not EditorActionType.ElseBlockStart)
            {
                continue;
            }

            if (index is 0 || actions[index - 1].Type is not EditorActionType.BlockEnd)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"Action {index + 1}: else block must come right after the closing brace of an if block."));
                continue;
            }

            if (!blockEndToStart.TryGetValue(index - 1, out var startIndex)
                || actions[startIndex].Type is not EditorActionType.IfBlockStart)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"Action {index + 1}: else block is only valid after an if block."));
            }
        }
    }

    private static bool HasEnclosingLoop(IEnumerable<(EditorActionType Type, int Index)> blockStack)
    {
        return blockStack.Any(entry => EditorActionScriptClassifier.IsLoopBlockStartAction(entry.Type));
    }
}
