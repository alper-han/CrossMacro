using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;

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

            if (type != EditorActionType.BlockEnd)
            {
                if (EditorActionScriptClassifier.IsLoopControlAction(type) && !HasEnclosingLoop(blockStack))
                {
                    errors.Add($"Action {index + 1}: {type} can only be used inside repeat/while/for blocks.");
                }

                continue;
            }

            if (blockStack.Count == 0)
            {
                errors.Add($"Action {index + 1}: unexpected block end '}}'.");
                continue;
            }

            var start = blockStack.Pop();
            blockEndToStart[index] = start.Index;
        }

        while (blockStack.Count > 0)
        {
            var unclosed = blockStack.Pop();
            errors.Add($"Action {unclosed.Index + 1}: block is not closed with a matching '}}'.");
        }

        for (var index = 0; index < actions.Count; index++)
        {
            if (actions[index].Type != EditorActionType.ElseBlockStart)
            {
                continue;
            }

            if (index == 0 || actions[index - 1].Type != EditorActionType.BlockEnd)
            {
                errors.Add($"Action {index + 1}: else block must come right after the closing brace of an if block.");
                continue;
            }

            if (!blockEndToStart.TryGetValue(index - 1, out var startIndex)
                || actions[startIndex].Type != EditorActionType.IfBlockStart)
            {
                errors.Add($"Action {index + 1}: else block is only valid after an if block.");
            }
        }

        return new ScriptBlockStructureValidationResult(errors);
    }

    private static bool HasEnclosingLoop(IEnumerable<(EditorActionType Type, int Index)> blockStack)
    {
        return blockStack.Any(entry => EditorActionScriptClassifier.IsLoopBlockStartAction(entry.Type));
    }
}

public sealed class ScriptBlockStructureValidationResult
{
    public ScriptBlockStructureValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<string> Errors { get; }
}
