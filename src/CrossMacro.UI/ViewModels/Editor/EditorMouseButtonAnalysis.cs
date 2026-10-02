namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Proves idle rows using possible held buttons on every branch and loop iteration.
/// Conditions are not evaluated; opaque script input and malformed structure are conservative.
/// </summary>
internal sealed class EditorMouseButtonAnalysis
{
    private const byte Reachable = 1 << 5;
    private readonly byte[] _incomingStates;

    private EditorMouseButtonAnalysis(byte[] incomingStates)
    {
        _incomingStates = incomingStates;
    }

    public bool IsDefinitelyIdle(int index) => _incomingStates[index] == Reachable;

    public static EditorMouseButtonAnalysis Create(IReadOnlyList<EditorAction> actions)
    {
        var incoming = new byte[actions.Count];
        if (actions.Count is 0)
        {
            return new EditorMouseButtonAnalysis(incoming);
        }

        var hasControlFlow = false;
        for (var index = 0; index < actions.Count; index++)
        {
            if (EditorActionScriptClassifier.IsScriptFlowControlAction(GetControlFlowType(actions[index])))
            {
                hasControlFlow = true;
                break;
            }
        }

        if (!hasControlFlow)
        {
            var state = default(EditorMouseButtonState);
            for (var index = 0; index < actions.Count; index++)
            {
                incoming[index] = (byte)(Reachable | state.PressedButtons);
                state.Update(actions[index]);
            }

            return new EditorMouseButtonAnalysis(incoming);
        }

        var next = new int[actions.Count];
        var alternative = new int[actions.Count];
        if (!TryBuildSuccessors(actions, next, alternative))
        {
            // No row is proven idle while the user is editing an incomplete/invalid block.
            return new EditorMouseButtonAnalysis(incoming);
        }

        var pending = new Queue<int>();
        var queued = new bool[actions.Count];
        incoming[0] = Reachable;
        pending.Enqueue(0);
        queued[0] = true;
        while (pending.TryDequeue(out var index))
        {
            queued[index] = false;
            var state = new EditorMouseButtonState((byte)(incoming[index] & ~Reachable));
            state.Update(actions[index]);
            var outgoing = (byte)(Reachable | state.PressedButtons);
            Propagate(next[index], outgoing, incoming, pending, queued);
            Propagate(alternative[index], outgoing, incoming, pending, queued);
        }

        return new EditorMouseButtonAnalysis(incoming);
    }

    private static void Propagate(int successor, byte outgoing, byte[] incoming, Queue<int> pending, bool[] queued)
    {
        if (successor < 0 || successor >= incoming.Length)
        {
            return;
        }

        var merged = (byte)(incoming[successor] | outgoing);
        if (merged == incoming[successor])
        {
            return;
        }

        incoming[successor] = merged;
        if (!queued[successor])
        {
            pending.Enqueue(successor);
            queued[successor] = true;
        }
    }

    private static EditorActionType GetControlFlowType(EditorAction action)
    {
        if (action.Type is EditorActionType.RawScriptStep)
        {
            var step = action.Text.AsSpan().Trim();
            if (step.Equals(RunScriptSyntax.BreakCommand, StringComparison.OrdinalIgnoreCase))
            {
                return EditorActionType.Break;
            }

            if (step.Equals(RunScriptSyntax.ContinueCommand, StringComparison.OrdinalIgnoreCase))
            {
                return EditorActionType.Continue;
            }
        }

        return action.Type;
    }

    private static bool TryBuildSuccessors(IReadOnlyList<EditorAction> actions, int[] next, int[] alternative)
    {
        var matching = new int[actions.Count];
        Array.Fill(matching, -1);
        Array.Fill(alternative, -1);
        var blocks = new Stack<(int Start, int OuterLoop)>();
        var enclosingLoop = -1;
        for (var index = 0; index < actions.Count; index++)
        {
            next[index] = index + 1;
            var type = GetControlFlowType(actions[index]);
            if (EditorActionScriptClassifier.IsScriptBlockStartAction(type))
            {
                blocks.Push((index, enclosingLoop));
                if (EditorActionScriptClassifier.IsLoopBlockStartAction(type))
                {
                    enclosingLoop = index;
                }
            }
            else if (type is EditorActionType.BlockEnd)
            {
                if (!blocks.TryPop(out var block))
                {
                    return false;
                }

                matching[block.Start] = index;
                matching[index] = block.Start;
                enclosingLoop = block.OuterLoop;
            }
            else if (EditorActionScriptClassifier.IsLoopControlAction(type))
            {
                alternative[index] = enclosingLoop;
                if (enclosingLoop < 0)
                {
                    return false;
                }
            }
        }

        if (blocks.Count is not 0)
        {
            return false;
        }

        for (var index = 0; index < actions.Count; index++)
        {
            var type = GetControlFlowType(actions[index]);
            if (type is EditorActionType.IfBlockStart)
            {
                alternative[index] = matching[index] + 1;
                if (alternative[index] < actions.Count && actions[alternative[index]].Type is EditorActionType.ElseBlockStart)
                {
                    next[matching[index]] = matching[alternative[index]] + 1;
                }
            }
            else if (type is EditorActionType.ElseBlockStart)
            {
                if (index is 0 || actions[index - 1].Type is not EditorActionType.BlockEnd
                    || actions[matching[index - 1]].Type is not EditorActionType.IfBlockStart)
                {
                    return false;
                }
            }
            else if (EditorActionScriptClassifier.IsLoopBlockStartAction(type))
            {
                alternative[index] = matching[index] + 1;
                next[matching[index]] = index;
            }
            else if (EditorActionScriptClassifier.IsLoopControlAction(type))
            {
                var loopStart = alternative[index];
                next[index] = type is EditorActionType.Break ? matching[loopStart] + 1 : loopStart;
                alternative[index] = -1;
            }
        }

        return true;
    }
}
