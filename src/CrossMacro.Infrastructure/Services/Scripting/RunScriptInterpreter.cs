namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// The one interpreter of the script node tree. It owns control flow (<c>repeat</c>, <c>while</c>,
/// <c>for</c>, <c>if</c>/<c>else</c>, <c>break</c>, <c>continue</c>), the loop budget, condition and arithmetic
/// evaluation and the variable commands, and hands out only the commands it does not interpret itself. Static
/// expansion collects those commands; playback executes them, and what they do to the variables is seen by the
/// next <see cref="MoveNext"/>. Pulling one command at a time is what lets both surfaces share this code.
/// </summary>
internal sealed class RunScriptInterpreter
{
    private const string BreakOutsideLoop = "'break' can only be used inside repeat/while/for blocks.";
    private const string ContinueOutsideLoop = "'continue' can only be used inside repeat/while/for blocks.";

    private readonly List<Frame> _frames = [];
    private readonly RunScriptVariables _variables;
    private readonly RunScriptDialect _dialect;
    private readonly CancellationToken _cancellationToken;
    private int _iterations;
    private int _loopDepth;

    public RunScriptInterpreter(
        IReadOnlyList<RunScriptNode> program,
        RunScriptVariables variables,
        RunScriptDialect dialect,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _cancellationToken = cancellationToken;
        _frames.Add(new Frame(program, loop: null));
    }

    public enum Status
    {
        /// <summary>A command the interpreter does not handle itself is ready.</summary>
        Command,
        Completed,
        /// <summary>The script cannot continue; <see cref="FailureMessage"/> says why.</summary>
        Failed,
    }

    /// <summary>The failure, worded and located for this interpreter's dialect.</summary>
    public string FailureMessage { get; private set; } = string.Empty;

    /// <summary>Advances to the next command to execute, interpreting everything before it.</summary>
    public Status MoveNext(out CommandNode? command)
    {
        command = null;
        while (_frames.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var frame = _frames[^1];
            if (frame.Next >= frame.Body.Count)
            {
                if (!FinishBody(frame))
                {
                    return Status.Failed;
                }

                continue;
            }

            var node = frame.Body[frame.Next++];
            var flow = node switch
            {
                CommandNode commandNode => Step(commandNode, ref command),
                RepeatNode repeat => EnterRepeat(repeat),
                WhileNode whileNode => PushLoop(new Frame(whileNode.Body, whileNode)),
                ForNode forNode => EnterFor(forNode),
                IfNode ifNode => EnterIf(ifNode),
                _ => Fail(node.Source, "Internal parser error: unsupported script node."),
            };
            switch (flow)
            {
                case Flow.Yield:
                    return Status.Command;
                case Flow.Failed:
                    return Status.Failed;
            }
        }

        return Status.Completed;
    }

    private Flow Step(CommandNode command, ref CommandNode? yielded)
    {
        var step = command.Source.Step.Trim();
        if (RunScriptSyntax.IsBreakCommand(step))
        {
            return _loopDepth is 0 ? Fail(command.Source, BreakOutsideLoop) : ExitLoop();
        }

        if (RunScriptSyntax.IsContinueCommand(step))
        {
            return _loopDepth is 0 ? Fail(command.Source, ContinueOutsideLoop) : SkipToNextIteration();
        }

        if (RunScriptVariableCommands.TryParseAssignment(step, out var assignment, out var error))
        {
            return error is null ? Assign(command.Source, assignment) : Fail(command.Source, error);
        }

        if (RunScriptVariableCommands.TryParseUpdate(step, out var update, out error))
        {
            return error is null ? Update(command.Source, update) : Fail(command.Source, error);
        }

        yielded = command;
        return Flow.Yield;
    }

    private Flow Assign(RunScriptStep source, VariableAssignment assignment)
    {
        if (!_variables.TryResolveText(assignment.ValueToken, _dialect, out var resolved, out var resolutionError))
        {
            return Fail(source, resolutionError.Message);
        }

        if (!RunScriptVariableCommands.TryEvaluateAssignedValue(resolved, _variables, out var stored, out var error))
        {
            return Fail(source, error!);
        }

        _variables.Set(assignment.Name, stored);
        return Flow.Next;
    }

    private Flow Update(RunScriptStep source, VariableUpdate update)
    {
        _ = _variables.TryGetValue(update.Name, out var currentValue);
        if (!RunScriptVariableCommands.TryReadInteger(update, currentValue, out var current, out var error))
        {
            return Fail(source, error!);
        }

        if (!_dialect.TryResolveInteger(_variables, update.AmountToken, update.AmountLabel, out var amount, out var amountError))
        {
            return Fail(source, amountError);
        }

        if (!RunScriptVariableCommands.TryApply(update, current, amount, out var updated, out error))
        {
            return Fail(source, error!);
        }

        _variables.Set(update.Name, updated.ToString(CultureInfo.InvariantCulture));
        return Flow.Next;
    }

    private Flow EnterRepeat(RepeatNode repeat)
    {
        if (!TryEvaluateBlockInteger(repeat.CountToken, "repeat count", out var count, out var error))
        {
            return Fail(repeat.Source, error);
        }

        return count < 0
            ? Fail(repeat.Source, _dialect.NegativeRepeatCount)
            : PushLoop(new Frame(repeat.Body, repeat) { Limit = count });
    }

    private Flow EnterFor(ForNode forNode)
    {
        if (!TryEvaluateBlockInteger(forNode.StartToken, "for start", out var start, out var error)
            || !TryEvaluateBlockInteger(forNode.EndToken, "for end", out var end, out error))
        {
            return Fail(forNode.Source, error);
        }

        var step = start <= end ? 1 : -1;
        if (forNode.HasExplicitStep && !TryEvaluateBlockInteger(forNode.StepToken!, "for step", out step, out error))
        {
            return Fail(forNode.Source, error);
        }

        return step is 0
            ? Fail(forNode.Source, _dialect.ZeroForStep)
            : PushLoop(new Frame(forNode.Body, forNode) { Counter = start, Limit = end, Step = step });
    }

    private Flow EnterIf(IfNode ifNode)
    {
        if (!TryEvaluateCondition(ifNode.Condition, out var holds, out var error))
        {
            return Fail(ifNode.Source, error);
        }

        var branch = holds ? ifNode.TrueBody : ifNode.FalseBody;
        if (branch is { Count: > 0 })
        {
            _frames.Add(new Frame(branch, loop: null));
        }

        return Flow.Next;
    }

    // The loop frame starts past its body so the first pass goes through BeginIteration like every later one.
    private Flow PushLoop(Frame frame)
    {
        frame.Next = frame.Body.Count;
        _frames.Add(frame);
        _loopDepth++;
        return Flow.Next;
    }

    private Flow ExitLoop()
    {
        while (_frames[^1].Loop is null)
        {
            PopFrame();
        }

        PopFrame();
        return Flow.Next;
    }

    private Flow SkipToNextIteration()
    {
        while (_frames[^1].Loop is null)
        {
            PopFrame();
        }

        _frames[^1].Next = _frames[^1].Body.Count;
        return Flow.Next;
    }

    /// <summary>Called when a frame's body is exhausted: pops branches, restarts or ends loops.</summary>
    private bool FinishBody(Frame frame)
    {
        if (frame.Loop is null)
        {
            PopFrame();
            return true;
        }

        switch (BeginIteration(frame))
        {
            case IterationStart.Run:
                frame.Next = 0;
                return true;
            case IterationStart.Exit:
                PopFrame();
                return true;
            default:
                return false;
        }
    }

    private IterationStart BeginIteration(Frame frame)
    {
        var loop = frame.Loop!;
        switch (loop)
        {
            case RepeatNode:
                if (frame.Counter >= frame.Limit)
                {
                    return IterationStart.Exit;
                }

                frame.Counter++;
                break;
            case WhileNode whileNode:
                if (!TryEvaluateCondition(whileNode.Condition, out var holds, out var error))
                {
                    _ = Fail(loop.Source, error);
                    return IterationStart.Failed;
                }

                if (!holds)
                {
                    return IterationStart.Exit;
                }

                break;
            case ForNode:
                if (frame.Step > 0 ? frame.Counter > frame.Limit : frame.Counter < frame.Limit)
                {
                    return IterationStart.Exit;
                }

                break;
        }

        if (++_iterations > _dialect.IterationLimit)
        {
            _ = Fail(loop.Source, _dialect.LoopLimitExceeded);
            return IterationStart.Failed;
        }

        if (loop is ForNode forNode)
        {
            _variables.Set(forNode.VariableName, frame.Counter.ToString(CultureInfo.InvariantCulture));
            frame.Counter += frame.Step;
        }

        return IterationStart.Run;
    }

    // Block arguments accept one binary expression; plain tokens keep the dialect's integer rule.
    private bool TryEvaluateBlockInteger(string token, string description, out int value, out string error)
    {
        var evaluation = ScriptNumericExpression.Evaluate(token, _variables, description);
        switch (evaluation.Status)
        {
            case ScriptNumericExpressionStatus.Evaluated:
                value = evaluation.Value;
                error = string.Empty;
                return true;
            case ScriptNumericExpressionStatus.NotExpression:
                return _dialect.TryResolveInteger(_variables, token, description, out value, out error);
            default:
                value = 0;
                error = evaluation.Error!;
                return false;
        }
    }

    private bool TryEvaluateCondition(ConditionExpression condition, out bool holds, out string error)
    {
        var result = ScriptConditionEvaluator.Evaluate(condition.LeftToken, condition.OperatorToken, condition.RightToken, _variables);
        holds = result.Value;
        error = result.Success ? string.Empty : _dialect.ConditionFailure(result);
        return result.Success;
    }

    private Flow Fail(RunScriptStep source, string message)
    {
        FailureMessage = _dialect.FormatFailure(source, message);
        return Flow.Failed;
    }

    private void PopFrame()
    {
        if (_frames[^1].Loop is not null)
        {
            _loopDepth--;
        }

        _frames.RemoveAt(_frames.Count - 1);
    }

    private enum Flow
    {
        Next,
        Yield,
        Failed,
    }

    private enum IterationStart
    {
        Run,
        Exit,
        Failed,
    }

    private sealed class Frame(IReadOnlyList<RunScriptNode> body, RunScriptNode? loop)
    {
        public IReadOnlyList<RunScriptNode> Body { get; } = body;

        /// <summary>The loop this frame is the body of; null for the program and for <c>if</c> branches.</summary>
        public RunScriptNode? Loop { get; } = loop;

        public int Next { get; set; }

        /// <summary><c>repeat</c>: iterations started so far. <c>for</c>: the value of the next iteration.</summary>
        public int Counter { get; set; }

        /// <summary><c>repeat</c>: the iteration count. <c>for</c>: the inclusive end value.</summary>
        public int Limit { get; init; }

        public int Step { get; init; }
    }
}
