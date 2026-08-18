
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Compiles run-script style steps (set/repeat/if/while/for + event commands)
/// into executable MacroSequence.
/// </summary>
public sealed class RunScriptCompiler
{
    private const int MaxLoopIterations = 100_000;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptRuntimeValidator _runtimeValidator;

    public RunScriptCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _runtimeValidator = new RunScriptRuntimeValidator(CompileStaticCommand);
    }

    public RunScriptCompileResult Compile(IReadOnlyList<RunScriptStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var parseResult = ParseScriptNodes(steps);
        if (!parseResult.Success)
        {
            return RunScriptCompileResult.Fail(parseResult.ErrorMessage);
        }

        if (RunScriptRuntimeValidator.ContainsRuntimeServiceNode(parseResult.Nodes!))
        {
            return CompileRuntimeScriptBackedSteps(steps, parseResult.Nodes!);
        }

        var expandedSteps = new List<RunScriptStep>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var loopState = new LoopExecutionState();
        var expandResult = ExpandScriptNodes(parseResult.Nodes!, variables, expandedSteps, loopState, loopDepth: 0);
        if (!expandResult.Success)
        {
            return RunScriptCompileResult.Fail(expandResult.ErrorMessage);
        }

        if (expandResult.LoopControlSignal is not LoopControlSignal.None)
        {
            return RunScriptCompileResult.Fail("Internal parser error: unhandled loop-control signal.");
        }

        if (expandedSteps.Count > 0
            && expandedSteps.TrueForAll(static step => step.Step.TrimStart().StartsWith("delay ", StringComparison.OrdinalIgnoreCase)))
        {
            return CompileRuntimeScriptBackedSteps(steps, parseResult.Nodes!);
        }

        if (expandedSteps.Count is 0 && RunScriptRuntimeValidator.ContainsRuntimeBackedNode(parseResult.Nodes!))
        {
            return CompileRuntimeScriptBackedSteps(steps, parseResult.Nodes!);
        }

        return CompileExpandedSteps(expandedSteps);
    }

    private RunScriptCompileResult CompileRuntimeScriptBackedSteps(IReadOnlyList<RunScriptStep> steps, IReadOnlyList<RunScriptNode> nodes)
    {
        var validation = _runtimeValidator.Validate(nodes, loopDepth: 0);
        if (!validation.Success)
        {
            return RunScriptCompileResult.Fail(validation.ErrorMessage);
        }

        var sequence = new MacroSequence
        {
            Name = "Run Script",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
        };
        sequence.ReplaceScriptSteps(steps
            .Select(step => step.Step.Trim())
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList());

        return RunScriptCompileResult.Ok(sequence, initialDelayMicroseconds: 0);
    }

    private RunScriptCompileResult CompileStaticCommand(RunScriptStep step)
    {
        return CompileExpandedSteps([step]);
    }

    private RunScriptCompileResult CompileExpandedSteps(List<RunScriptStep> expandedSteps)
    {
        var sequence = new MacroSequence
        {
            Name = "Run Script",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
        };

        var timestampMicroseconds = 0L;
        long pendingFixedDelayMicroseconds = 0;
        var pendingHasRandomDelay = false;
        var pendingRandomDelayMinMs = 0;
        var pendingRandomDelayMaxMs = 0;
        long initialFixedDelayMicroseconds = 0;
        var initialHasRandomDelay = false;
        var initialRandomDelayMinMs = 0;
        var initialRandomDelayMaxMs = 0;
        var hasEvents = false;
        var hasScreenReadingSteps = false;
        MouseCoordinateMode? currentMoveMode = null;
        MouseCoordinateSpace? currentMoveCoordinateSpace = null;
        var hasAbsoluteCursorPosition = false;
        var absoluteCursorX = 0;
        var absoluteCursorY = 0;

        for (var i = 0; i < expandedSteps.Count; i++)
        {
            var stepNumber = i + 1;
            var stepEntry = expandedSteps[i];
            var rawStep = stepEntry.Step;
            var lineNumber = stepEntry.SourceLineNumber;
            var stepPrefix = lineNumber is not null ? $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)} (line {lineNumber.Value.ToString(CultureInfo.InvariantCulture)})"
                : $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}";

            if (string.IsNullOrWhiteSpace(rawStep))
            {
                return RunScriptCompileResult.Fail($"{stepPrefix}: step cannot be empty.");
            }

            var step = rawStep.Trim();
            var stepForType = rawStep.TrimStart();
            if (TryParseDelay(step, out var hasRandomDelay, out var fixedDelayMicroseconds, out var randomDelayMinMs, out var randomDelayMaxMs, out var delayError))
            {
                if (delayError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {delayError}");
                }

                if (!hasEvents)
                {
                    initialFixedDelayMicroseconds += fixedDelayMicroseconds;
                    if (hasRandomDelay)
                    {
                        initialHasRandomDelay = true;
                        initialRandomDelayMinMs += randomDelayMinMs;
                        initialRandomDelayMaxMs += randomDelayMaxMs;
                    }
                }
                else
                {
                    pendingFixedDelayMicroseconds += fixedDelayMicroseconds;
                    if (hasRandomDelay)
                    {
                        pendingHasRandomDelay = true;
                        pendingRandomDelayMinMs += randomDelayMinMs;
                        pendingRandomDelayMaxMs += randomDelayMaxMs;
                    }
                }

                continue;
            }

            if (TryParseMove(
                step,
                out var coordinateMode,
                out var coordinateSpace,
                out var x,
                out var y,
                out var moveError))
            {
                if (moveError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {moveError}");
                }

                currentMoveMode = coordinateMode;
                currentMoveCoordinateSpace = coordinateSpace;
                EmitEvent(new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = x,
                    Y = y,
                    CoordinateMode = coordinateMode,
                    CoordinateSpace = coordinateSpace,
                });

                if (coordinateMode is MouseCoordinateMode.Absolute)
                {
                    hasAbsoluteCursorPosition = true;
                    absoluteCursorX = x;
                    absoluteCursorY = y;
                }
                else
                {
                    hasAbsoluteCursorPosition = false;
                }

                continue;
            }

            if (TryEmitButton(step, "down", EventType.ButtonPress, out var buttonError)
                || TryEmitButton(step, "up", EventType.ButtonRelease, out buttonError)
                || TryEmitButton(step, "click", EventType.Click, out buttonError))
            {
                if (buttonError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {buttonError}");
                }

                continue;
            }

            if (TryParseScroll(step, out var scrollButton, out var scrollCount, out var scrollError))
            {
                if (scrollError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {scrollError}");
                }

                for (var c = 0; c < scrollCount; c++)
                {
                    EmitEvent(new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = scrollButton,
                    });
                }

                continue;
            }

            if (TryParseScreenReadingStep(step, out var screenReadingError))
            {
                if (screenReadingError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {screenReadingError}");
                }

                hasScreenReadingSteps = true;
                continue;
            }

            if (TryParseKey(step, out var isKeyDown, out var keyToken, out var keyError))
            {
                if (keyError is not null)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: {keyError}");
                }

                var keyCode = ResolveKeyCode(keyToken);
                if (keyCode < 0)
                {
                    return RunScriptCompileResult.Fail($"{stepPrefix}: unknown key '{keyToken}'.");
                }

                EmitEvent(new MacroEvent
                {
                    Type = isKeyDown ? EventType.KeyPress : EventType.KeyRelease,
                    KeyCode = keyCode,
                });

                continue;
            }

            if (TryEmitTapCombo(stepPrefix, step, EmitEvent, out var tapError))
            {
                if (tapError is not null)
                {
                    return RunScriptCompileResult.Fail(tapError);
                }

                continue;
            }

            if (TryEmitTypeText(stepPrefix, stepForType, EmitEvent, out var typeError))
            {
                if (typeError is not null)
                {
                    return RunScriptCompileResult.Fail(typeError);
                }

                continue;
            }

            return RunScriptCompileResult.Fail($"{stepPrefix}: unsupported step syntax '{rawStep}'.");
        }

        if (!hasEvents && !hasScreenReadingSteps)
        {
            return RunScriptCompileResult.Fail(
                "Run script did not produce any executable events. Add at least one runtime step (move/click/down/up/scroll/key/tap/type).");
        }

        if (hasScreenReadingSteps)
        {
            sequence.ReplaceScriptSteps(expandedSteps
                .Select(step => step.Step.Trim())
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .ToList());
        }

        sequence.IsAbsoluteCoordinates = MacroPositionSemantics.GetCoordinateModeSummary(sequence) is CoordinateModeSummary.Absolute;
        sequence.TrailingDelayMicroseconds = pendingFixedDelayMicroseconds;
        sequence.HasTrailingRandomDelay = pendingHasRandomDelay;
        sequence.TrailingDelayMinMs = pendingRandomDelayMinMs;
        sequence.TrailingDelayMaxMs = pendingRandomDelayMaxMs;
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type is EventType.Click or EventType.ButtonPress or EventType.ButtonRelease);
        sequence.CalculateDuration();

        return RunScriptCompileResult.Ok(
            sequence,
            initialFixedDelayMicroseconds,
            initialHasRandomDelay,
            initialRandomDelayMinMs,
            initialRandomDelayMaxMs);

        void EmitEvent(MacroEvent ev)
        {
            ev.DelayMicroseconds = pendingFixedDelayMicroseconds;
            ev.HasRandomDelay = pendingHasRandomDelay;
            ev.RandomDelayMinMs = pendingRandomDelayMinMs;
            ev.RandomDelayMaxMs = pendingRandomDelayMaxMs;
            timestampMicroseconds += pendingFixedDelayMicroseconds;
            if (pendingHasRandomDelay)
            {
                timestampMicroseconds += (long)pendingRandomDelayMinMs * MacroTiming.MicrosecondsPerMillisecond;
            }

            ev.TimestampMicroseconds = timestampMicroseconds;
            pendingFixedDelayMicroseconds = 0;
            pendingHasRandomDelay = false;
            pendingRandomDelayMinMs = 0;
            pendingRandomDelayMaxMs = 0;
            sequence.Events.Add(ev);
            hasEvents = true;
        }

        bool TryEmitButton(string stepToParse, string command, EventType eventType, out string? error)
        {
            error = null;
            if (!TryParseButton(stepToParse, command, out var button, out var useCurrentPosition, out var buttonError))
            {
                return false;
            }

            if (buttonError is not null)
            {
                error = buttonError;
                return true;
            }

            var buttonEvent = new MacroEvent
            {
                Type = eventType,
                Button = button,
                UseCurrentPosition = useCurrentPosition || currentMoveMode is null,
            };

            if (useCurrentPosition)
            {
                EmitEvent(buttonEvent);
                return true;
            }

            if (currentMoveMode is MouseCoordinateMode.Absolute)
            {
                if (!hasAbsoluteCursorPosition)
                {
                    error = $"{command} <button> requires a prior 'move abs <x> <y>' step in absolute mode.";
                    return true;
                }

                buttonEvent.X = absoluteCursorX;
                buttonEvent.Y = absoluteCursorY;
                buttonEvent.CoordinateMode = MouseCoordinateMode.Absolute;
                buttonEvent.CoordinateSpace = MouseCoordinateSpace.LogicalDesktop;
            }
            else if (currentMoveMode is MouseCoordinateMode.Relative)
            {
                buttonEvent.CoordinateMode = MouseCoordinateMode.Relative;
                buttonEvent.CoordinateSpace = currentMoveCoordinateSpace ?? MouseCoordinateSpace.LogicalDesktop;
            }

            EmitEvent(buttonEvent);
            return true;
        }
    }

    private static ScriptNodeParseResult ParseScriptNodes(IReadOnlyList<RunScriptStep> steps)
    {
        var index = 0;
        var result = ParseBlockNodes(steps, ref index, isTopLevel: true);
        if (!result.Success)
        {
            return result;
        }

        return ScriptNodeParseResult.Ok(result.Nodes!);
    }

    private static ScriptNodeParseResult ParseBlockNodes(IReadOnlyList<RunScriptStep> steps, ref int index, bool isTopLevel)
    {
        var nodes = new List<RunScriptNode>();

        while (index < steps.Count)
        {
            var entry = steps[index];
            var trimmed = entry.Step.Trim();
            var source = BuildSourcePrefix(entry);

            if (RunScriptSyntax.IsBlockEndToken(trimmed))
            {
                if (isTopLevel)
                {
                    return ScriptNodeParseResult.Fail($"{source}: unexpected closing brace '}}'.");
                }

                index++;
                return ScriptNodeParseResult.Ok(nodes);
            }

            if (RunScriptSyntax.IsElseHeader(trimmed))
            {
                return ScriptNodeParseResult.Fail($"{source}: unexpected 'else' block.");
            }

            if (RunScriptHeaderParser.TryParseRepeatCountToken(trimmed, out var repeatCountToken))
            {
                var repeatSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new RepeatNode(repeatSource, repeatCountToken, bodyResult.Nodes!));
                continue;
            }

            if (TryParseIfHeader(trimmed, out var ifCondition, out var ifHeaderError))
            {
                if (ifHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {ifHeaderError}");
                }

                var ifSource = entry;
                index++;
                var trueBodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!trueBodyResult.Success)
                {
                    return trueBodyResult;
                }

                RunScriptStep? elseSource = null;
                IReadOnlyList<RunScriptNode>? falseBody = null;
                if (index < steps.Count && RunScriptSyntax.IsElseHeader(steps[index].Step.Trim()))
                {
                    elseSource = steps[index];
                    index++;

                    var falseBodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                    if (!falseBodyResult.Success)
                    {
                        return falseBodyResult;
                    }

                    falseBody = falseBodyResult.Nodes!;
                }

                nodes.Add(new IfNode(ifSource, ifCondition!, trueBodyResult.Nodes!, elseSource, falseBody));
                continue;
            }

            if (TryParseWhileHeader(trimmed, out var whileCondition, out var whileHeaderError))
            {
                if (whileHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {whileHeaderError}");
                }

                var whileSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new WhileNode(whileSource, whileCondition!, bodyResult.Nodes!));
                continue;
            }

            if (RunScriptHeaderParser.TryParseForHeader(trimmed, out var forHeader, out var forHeaderError))
            {
                if (forHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {forHeaderError}");
                }

                var forSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new ForNode(
                    forSource,
                    forHeader!.VariableName,
                    forHeader.StartToken,
                    forHeader.EndToken,
                    forHeader.StepToken,
                    forHeader.HasExplicitStep,
                    bodyResult.Nodes!));
                continue;
            }

            if (trimmed.EndsWith('{'))
            {
                return ScriptNodeParseResult.Fail(
                    $"{source}: unsupported block syntax. Expected one of: repeat <count> {{, if <left> <op> <right> {{, while <left> <op> <right> {{, for <var> from <start> to <end> [step <n>] {{");
            }

            nodes.Add(new CommandNode(entry));
            index++;
        }

        if (!isTopLevel)
        {
            return ScriptNodeParseResult.Fail("Missing closing brace '}' for block.");
        }

        return ScriptNodeParseResult.Ok(nodes);
    }

    private static ScriptExpansionResult ExpandScriptNodes(
        IReadOnlyList<RunScriptNode> nodes,
        Dictionary<string, string> variables,
        List<RunScriptStep> output,
        LoopExecutionState loopState,
        int loopDepth)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CommandNode command:
                    {
                        var rawStep = command.Source.Step;
                        var step = rawStep.Trim();
                        var source = BuildSourcePrefix(command.Source);

                        if (RunScriptSyntax.IsBreakCommand(step))
                        {
                            if (loopDepth is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: 'break' can only be used inside repeat/while/for blocks.");
                            }

                            return ScriptExpansionResult.Break();
                        }

                        if (RunScriptSyntax.IsContinueCommand(step))
                        {
                            if (loopDepth is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: 'continue' can only be used inside repeat/while/for blocks.");
                            }

                            return ScriptExpansionResult.Continue();
                        }

                        if (TryParseSetCommand(step, out var variableName, out var variableValue, out var setError))
                        {
                            if (!string.IsNullOrEmpty(setError))
                            {
                                return ScriptExpansionResult.Fail($"{source}: {setError}");
                            }

                            var resolvedValueResult = ResolveVariables(variableValue, variables);
                            if (!resolvedValueResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {resolvedValueResult.ErrorMessage}");
                            }

                            // A surviving '$' is a '$$' escape; keep the raw-string fallback.
                            var resolvedValue = resolvedValueResult.Value!;
                            if (!resolvedValue.Contains('$', StringComparison.Ordinal)
                                && ScriptNumericExpression.TryParse(resolvedValue, out var numericExpression)
                                && numericExpression is not null)
                            {
                                if (!ScriptNumericExpression.Evaluate(numericExpression, variables, out var numericValue, out var expressionError))
                                {
                                    return ScriptExpansionResult.Fail($"{source}: {expressionError}");
                                }

                                variables[variableName!] = numericValue.ToString(CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                variables[variableName!] = resolvedValue;
                            }

                            break;
                        }

                        if (TryParseIncDecCommand(step, out var targetVariableName, out var amountToken, out var sign, out var incDecError))
                        {
                            if (incDecError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {incDecError}");
                            }

                            if (!variables.TryGetValue(targetVariableName!, out var existingValue)
                                || !int.TryParse(existingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existingInt))
                            {
                                return ScriptExpansionResult.Fail($"{source}: variable '{targetVariableName}' must exist and be an integer for inc/dec.");
                            }

                            var amountResult = ResolveIntegerToken(amountToken!, variables, "inc/dec amount");
                            if (!amountResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {amountResult.ErrorMessage}");
                            }

                            var updated = existingInt + (sign * amountResult.Value);
                            variables[targetVariableName!] = updated.ToString(CultureInfo.InvariantCulture);
                            break;
                        }

                        if (TryParseMulDivCommand(step, out var mulDivVariableName, out var mulDivAmountToken, out var isDivide, out var mulDivError))
                        {
                            if (mulDivError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {mulDivError}");
                            }

                            if (!variables.TryGetValue(mulDivVariableName!, out var mulDivExistingValue)
                                || !int.TryParse(mulDivExistingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mulDivExistingInt))
                            {
                                return ScriptExpansionResult.Fail($"{source}: variable '{mulDivVariableName}' must exist and be an integer for mul/div.");
                            }

                            var mulDivAmountResult = ResolveIntegerToken(mulDivAmountToken!, variables, "mul/div amount");
                            if (!mulDivAmountResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {mulDivAmountResult.ErrorMessage}");
                            }

                            if (isDivide && mulDivAmountResult.Value is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: Division by zero is not allowed in mul/div.");
                            }

                            var mulDivResult = isDivide
                                ? (long)mulDivExistingInt / mulDivAmountResult.Value
                                : (long)mulDivExistingInt * mulDivAmountResult.Value;
                            if (mulDivResult is < int.MinValue or > int.MaxValue)
                            {
                                return ScriptExpansionResult.Fail($"{source}: Result is out of range for mul/div.");
                            }

                            variables[mulDivVariableName!] = ((int)mulDivResult).ToString(CultureInfo.InvariantCulture);
                            break;
                        }

                        var resolvedStepResult = ResolveVariables(rawStep, variables);
                        if (!resolvedStepResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {resolvedStepResult.ErrorMessage}");
                        }

                        output.Add(command.Source with { Step = resolvedStepResult.Value! });
                        break;
                    }
                case RepeatNode repeat:
                    {
                        var source = BuildSourcePrefix(repeat.Source);
                        var repeatCountResult = ResolveBlockArgumentToken(repeat.CountToken, variables, "repeat count");
                        if (!repeatCountResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {repeatCountResult.ErrorMessage}");
                        }

                        if (repeatCountResult.Value < 0)
                        {
                            return ScriptExpansionResult.Fail($"{source}: repeat count must be >= 0.");
                        }

                        for (var i = 0; i < repeatCountResult.Value; i++)
                        {
                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            var nestedResult = ExpandScriptNodes(repeat.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                case IfNode ifNode:
                    {
                        var source = BuildSourcePrefix(ifNode.Source);
                        var conditionResult = EvaluateCondition(ifNode.Condition, variables);
                        if (!conditionResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {conditionResult.ErrorMessage}");
                        }

                        var branch = conditionResult.Value ? ifNode.TrueBody : ifNode.FalseBody;
                        if (branch is null || branch.Count is 0)
                        {
                            break;
                        }

                        var nestedResult = ExpandScriptNodes(branch, variables, output, loopState, loopDepth);
                        if (!nestedResult.Success)
                        {
                            return nestedResult;
                        }

                        if (nestedResult.LoopControlSignal is not LoopControlSignal.None)
                        {
                            return nestedResult;
                        }

                        break;
                    }
                case WhileNode whileNode:
                    {
                        var source = BuildSourcePrefix(whileNode.Source);
                        while (true)
                        {
                            var conditionResult = EvaluateCondition(whileNode.Condition, variables);
                            if (!conditionResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {conditionResult.ErrorMessage}");
                            }

                            if (!conditionResult.Value)
                            {
                                break;
                            }

                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            var nestedResult = ExpandScriptNodes(whileNode.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                case ForNode forNode:
                    {
                        var source = BuildSourcePrefix(forNode.Source);
                        var startResult = ResolveBlockArgumentToken(forNode.StartToken, variables, "for start");
                        if (!startResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {startResult.ErrorMessage}");
                        }

                        var endResult = ResolveBlockArgumentToken(forNode.EndToken, variables, "for end");
                        if (!endResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {endResult.ErrorMessage}");
                        }

                        int stepValue;
                        if (forNode.HasExplicitStep)
                        {
                            var stepResult = ResolveBlockArgumentToken(forNode.StepToken!, variables, "for step");
                            if (!stepResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {stepResult.ErrorMessage}");
                            }

                            stepValue = stepResult.Value;
                        }
                        else
                        {
                            stepValue = startResult.Value <= endResult.Value ? 1 : -1;
                        }

                        if (stepValue is 0)
                        {
                            return ScriptExpansionResult.Fail($"{source}: for step cannot be 0.");
                        }

                        for (var i = startResult.Value;
                             stepValue > 0 ? i <= endResult.Value : i >= endResult.Value;
                             i += stepValue)
                        {
                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            variables[forNode.VariableName] = i.ToString(CultureInfo.InvariantCulture);
                            var nestedResult = ExpandScriptNodes(forNode.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                default:
                    return ScriptExpansionResult.Fail("Internal parser error: unsupported script node.");
            }
        }

        return ScriptExpansionResult.Ok();
    }

    private static bool TryAdvanceLoopIteration(LoopExecutionState loopState, string source, out string? error)
    {
        loopState.Iterations++;
        if (loopState.Iterations > MaxLoopIterations)
        {
            error = $"{source}: loop iteration limit exceeded ({MaxLoopIterations}). Check loop exit condition.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseIfHeader(string step, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        error = null;

        if (!step.EndsWith('{') || !step.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[..^1].Trim();
        payload = payload[2..].Trim();
        if (!TryParseConditionExpression(payload, out condition, out error))
        {
            return true;
        }

        return true;
    }

    private static bool TryParseWhileHeader(string step, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        error = null;

        if (!step.EndsWith('{') || !step.StartsWith("while ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[..^1].Trim();
        payload = payload[5..].Trim();
        if (!TryParseConditionExpression(payload, out condition, out error))
        {
            return true;
        }

        return true;
    }

    private static bool TryParseConditionExpression(string payload, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        if (!RunScriptConditionParser.TryParse(payload, out var parsedCondition, out error) || parsedCondition == null)
        {
            return false;
        }

        condition = new ConditionExpression(
            parsedCondition.LeftToken,
            parsedCondition.OperatorToken,
            parsedCondition.RightToken);
        return true;
    }

    private static ConditionEvaluationResult EvaluateCondition(
        ConditionExpression condition,
        IReadOnlyDictionary<string, string> variables)
    {
        if (condition.OperatorToken is "==" or "!=")
        {
            // String/boolean/color comparison: no arithmetic on this path.
            var leftEqualsResult = ResolveOperandValue(condition.LeftToken, variables);
            if (!leftEqualsResult.Success)
            {
                return ConditionEvaluationResult.Fail(leftEqualsResult.ErrorMessage);
            }

            var rightEqualsResult = ResolveOperandValue(condition.RightToken, variables);
            if (!rightEqualsResult.Success)
            {
                return ConditionEvaluationResult.Fail(rightEqualsResult.ErrorMessage);
            }

            var equals = ValuesEqual(leftEqualsResult.Value!, rightEqualsResult.Value!);
            return ConditionEvaluationResult.Ok(condition.OperatorToken is "==" ? equals : !equals);
        }

        // Numeric comparison: arithmetic operands evaluate via the Core authority; other operands keep the legacy path (messages byte-identical).
        var leftOperand = ResolveNumericConditionOperand(condition.LeftToken, variables);
        if (!leftOperand.Success)
        {
            return ConditionEvaluationResult.Fail(leftOperand.ErrorMessage);
        }

        var rightOperand = ResolveNumericConditionOperand(condition.RightToken, variables);
        if (!rightOperand.Success)
        {
            return ConditionEvaluationResult.Fail(rightOperand.ErrorMessage);
        }

        var leftDisplay = leftOperand.ResolvedValue ?? condition.LeftToken;
        var rightDisplay = rightOperand.ResolvedValue ?? condition.RightToken;
        if (!leftOperand.TryGetInteger(variables, out var leftInt)
            || !rightOperand.TryGetInteger(variables, out var rightInt))
        {
            return ConditionEvaluationResult.Fail(
                $"Operator '{condition.OperatorToken}' requires numeric operands. Got '{leftDisplay}' and '{rightDisplay}'.");
        }

        var result = condition.OperatorToken switch
        {
            ">" => leftInt > rightInt,
            ">=" => leftInt >= rightInt,
            "<" => leftInt < rightInt,
            "<=" => leftInt <= rightInt,
            _ => false,
        };

        return ConditionEvaluationResult.Ok(result);
    }

    private static NumericConditionOperand ResolveNumericConditionOperand(
        string token,
        IReadOnlyDictionary<string, string> variables)
    {
        if (ScriptNumericExpression.TryParse(token, out var expression) && expression is { Op: not null })
        {
            var evaluation = ScriptNumericExpression.Evaluate(token, variables, "condition operand");
            return evaluation.Status is ScriptNumericExpressionStatus.Evaluated
                ? NumericConditionOperand.FromEvaluated(evaluation.Value)
                : NumericConditionOperand.Fail(evaluation.Error!);
        }

        var resolved = ResolveOperandValue(token, variables);
        return resolved.Success
            ? NumericConditionOperand.FromResolved(resolved.Value!)
            : NumericConditionOperand.Fail(resolved.ErrorMessage);
    }

    private static bool ValuesEqual(string left, string right)
    {
        if (ScreenPixelColor.TryParse(left, out var leftColor)
            && ScreenPixelColor.TryParse(right, out var rightColor))
        {
            return leftColor.Equals(rightColor);
        }

        if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftInt)
            && int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightInt))
        {
            return leftInt == rightInt;
        }

        if (bool.TryParse(left, out var leftBool)
            && bool.TryParse(right, out var rightBool))
        {
            return leftBool == rightBool;
        }

        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static OperandResolutionResult ResolveOperandValue(
        string token,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OperandResolutionResult.Fail("Condition token cannot be empty.");
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            var escapedLiteral = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return OperandResolutionResult.Ok(Unquote(escapedLiteral));
        }

        var value = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!EditorActionScriptTokens.IsValidVariableName(variableName))
            {
                return OperandResolutionResult.Fail($"Invalid variable reference '{token}'.");
            }

            if (!variables.TryGetValue(variableName, out value!))
            {
                return OperandResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }

            value = Unquote(value);
            return OperandResolutionResult.Ok(value);
        }

        value = Unquote(value);
        value = EditorActionScriptTokens.UnescapeLiteralDollar(value);
        return OperandResolutionResult.Ok(value);
    }

    private static string Unquote(string input)
    {
        if (input.Length >= 2
            && ((input[0] == '"' && input[^1] == '"')
                || (input[0] == '\'' && input[^1] == '\'')))
        {
            return input[1..^1];
        }

        return input;
    }

    private static bool TryParseSetCommand(string step, out string? variableName, out string variableValue, out string? error)
    {
        variableName = null;
        variableValue = string.Empty;
        error = null;

        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length is 0)
        {
            error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
            return true;
        }

        var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
        if (equalIndex >= 0)
        {
            variableName = payload[..equalIndex].Trim();
            variableValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is not 2)
            {
                error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
                return true;
            }

            variableName = parts[0];
            variableValue = parts[1];
        }

        if (string.IsNullOrWhiteSpace(variableName))
        {
            error = "Variable name cannot be empty.";
            return true;
        }

        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        if (string.IsNullOrWhiteSpace(variableValue))
        {
            error = $"Variable '{variableName}' value cannot be empty.";
            return true;
        }

        return true;
    }

    private static bool TryParseIncDecCommand(
        string step,
        out string? variableName,
        out string? amountToken,
        out int sign,
        out string? error)
    {
        variableName = null;
        amountToken = null;
        sign = 0;
        error = null;

        if (!step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase)
            && !step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sign = step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        var command = sign > 0 ? "inc" : "dec";
        var payload = step[4..].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2)
        {
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        variableName = parts[0];
        amountToken = parts.Length is 2 ? parts[1] : "1";
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        return true;
    }

    private static bool TryParseMulDivCommand(
        string step,
        out string? variableName,
        out string? amountToken,
        out bool isDivide,
        out string? error)
    {
        variableName = null;
        amountToken = null;
        isDivide = false;
        error = null;

        if (!step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase)
            && !step.StartsWith("div ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        isDivide = step.StartsWith("div ", StringComparison.OrdinalIgnoreCase);
        var command = isDivide ? "div" : "mul";
        var payload = step[4..].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2)
        {
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        variableName = parts[0];
        amountToken = parts.Length is 2 ? parts[1] : "1";
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        return true;
    }

    private static VariableResolutionResult ResolveVariables(string input, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return VariableResolutionResult.Ok(input);
        }

        var output = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch != '$')
            {
                _ = output.Append(ch);
                continue;
            }

            if (i + 1 >= input.Length)
            {
                return VariableResolutionResult.Fail("Invalid variable reference '$'.");
            }

            var next = input[i + 1];
            if (next == '$')
            {
                _ = output.Append('$');
                i++;
                continue;
            }

            if (!EditorActionScriptTokens.IsVariableNameStart(next))
            {
                return VariableResolutionResult.Fail($"Invalid variable reference '${next}'.");
            }

            var j = i + 1;
            while (j < input.Length && EditorActionScriptTokens.IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            if (!variables.TryGetValue(variableName, out var value))
            {
                return VariableResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }

            _ = output.Append(value);
            i = j - 1;
        }

        return VariableResolutionResult.Ok(output.ToString());
    }

    private static IntegerResolutionResult ResolveBlockArgumentToken(
        string token,
        Dictionary<string, string> variables,
        string description)
    {
        // Block arguments accept one binary expression via the Core authority; committed expressions fail loudly, plain tokens keep the legacy path.
        var evaluation = ScriptNumericExpression.Evaluate(token, variables, description);
        return evaluation.Status switch
        {
            ScriptNumericExpressionStatus.Evaluated => IntegerResolutionResult.Ok(evaluation.Value),
            ScriptNumericExpressionStatus.NotExpression => ResolveIntegerToken(token, variables, description),
            ScriptNumericExpressionStatus.Malformed or ScriptNumericExpressionStatus.EvaluationError => IntegerResolutionResult.Fail(evaluation.Error!),
            _ => IntegerResolutionResult.Fail(evaluation.Error!),
        };
    }

    private static IntegerResolutionResult ResolveIntegerToken(
        string token,
        Dictionary<string, string> variables,
        string description)
    {
        var resolved = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!EditorActionScriptTokens.IsValidVariableName(variableName))
            {
                return IntegerResolutionResult.Fail($"Invalid {description} variable reference '{token}'.");
            }

            if (!variables.TryGetValue(variableName, out resolved!))
            {
                return IntegerResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }
        }

        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return IntegerResolutionResult.Fail($"Invalid {description} '{resolved}'. Expected integer.");
        }

        return IntegerResolutionResult.Ok(parsed);
    }

    private static string BuildSourcePrefix(RunScriptStep entry)
    {
        var index = entry.SourceIndex > 0 ? entry.SourceIndex : 1;
        return entry.SourceLineNumber is not null ? $"Step {index.ToString(CultureInfo.InvariantCulture)} (line {entry.SourceLineNumber.Value.ToString(CultureInfo.InvariantCulture)})"
            : $"Step {index.ToString(CultureInfo.InvariantCulture)}";
    }

    private int ResolveKeyCode(string keyToken)
    {
        if (int.TryParse(keyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode))
        {
            return parsedCode;
        }

        return _keyCodeMapper.GetKeyCode(keyToken);
    }

    private bool TryEmitTapCombo(string stepPrefix, string step, Action<MacroEvent> emitEvent, out string? error)
    {
        error = null;
        if (!TryParseTap(step, out var combo))
        {
            return false;
        }

        var comboParts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (comboParts.Length is 0)
        {
            error = $"{stepPrefix}: tap combo cannot be empty.";
            return true;
        }

        var modifiers = new List<int>();
        var primaryKeys = new List<int>();
        foreach (var part in comboParts)
        {
            var code = ResolveKeyCode(part);
            if (code < 0)
            {
                error = $"{stepPrefix}: unknown key '{part}' in tap combo.";
                return true;
            }

            if (_keyCodeMapper.IsModifierKeyCode(code))
            {
                modifiers.Add(code);
            }
            else
            {
                primaryKeys.Add(code);
            }
        }

        var distinctModifiers = modifiers.Distinct().ToList();
        if (primaryKeys.Count is 0 && distinctModifiers.Count is 1)
        {
            emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = distinctModifiers[0] });
            emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = distinctModifiers[0] });
            return true;
        }

        if (primaryKeys.Count is not 1)
        {
            error = $"{stepPrefix}: tap expects either exactly one non-modifier key (example: ctrl+c) or a single modifier key.";
            return true;
        }

        foreach (var modifier in distinctModifiers)
        {
            emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
        }

        emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = primaryKeys[0] });
        emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = primaryKeys[0] });

        for (var m = distinctModifiers.Count - 1; m >= 0; m--)
        {
            emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = distinctModifiers[m] });
        }

        return true;
    }

    private bool TryEmitTypeText(string stepPrefix, string stepForType, Action<MacroEvent> emitEvent, out string? error)
    {
        error = null;
        if (!TryParseType(stepForType, out var textToType))
        {
            return false;
        }

        if (textToType.Length is 0)
        {
            error = $"{stepPrefix}: type text cannot be empty.";
            return true;
        }

        for (var index = 0; index < textToType.Length; index++)
        {
            var ch = textToType[index];
            if (ch == '\r')
            {
                if (index + 1 < textToType.Length && textToType[index + 1] == '\n')
                {
                    index++;
                }

                if (!TryEmitTapKeyByName("Enter", emitEvent, out var carriageReturnError))
                {
                    error = $"{stepPrefix}: {carriageReturnError}";
                    return true;
                }

                continue;
            }

            if (ch == '\n')
            {
                if (!TryEmitTapKeyByName("Enter", emitEvent, out var lineFeedError))
                {
                    error = $"{stepPrefix}: {lineFeedError}";
                    return true;
                }

                continue;
            }

            if (ch == '\t')
            {
                if (!TryEmitTapKeyByName("Tab", emitEvent, out var tabError))
                {
                    error = $"{stepPrefix}: {tabError}";
                    return true;
                }

                continue;
            }

            if (ch == '\b')
            {
                if (!TryEmitTapKeyByName("Backspace", emitEvent, out var backspaceError))
                {
                    error = $"{stepPrefix}: {backspaceError}";
                    return true;
                }

                continue;
            }

            var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(ch);
            if (keyCode < 0)
            {
                error = $"{stepPrefix}: cannot map character '{ch}' for type command.";
                return true;
            }

            var modifiers = new List<int>(2);
            if (_keyCodeMapper.RequiresShift(ch))
            {
                modifiers.Add(ResolveKeyCode("Shift"));
            }

            if (_keyCodeMapper.RequiresAltGr(ch))
            {
                modifiers.Add(ResolveKeyCode("AltGr"));
            }

            foreach (var modifier in modifiers.Distinct())
            {
                if (modifier < 0)
                {
                    error = $"{stepPrefix}: required modifier key is not available for type command.";
                    return true;
                }

                emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
            }

            emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = keyCode });
            emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = keyCode });

            for (var m = modifiers.Count - 1; m >= 0; m--)
            {
                emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = modifiers[m] });
            }
        }

        return true;
    }

    private bool TryEmitTapKeyByName(string keyName, Action<MacroEvent> emitEvent, out string? error)
    {
        var code = ResolveKeyCode(keyName);
        if (code < 0)
        {
            error = $"Unknown key '{keyName}'.";
            return false;
        }

        emitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = code });
        emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = code });
        error = null;
        return true;
    }

    private static bool TryParseDelay(
        string step,
        out bool hasRandomDelay,
        out long fixedDelayMicroseconds,
        out int randomDelayMinMs,
        out int randomDelayMaxMs,
        out string? error)
    {
        hasRandomDelay = false;
        fixedDelayMicroseconds = 0;
        randomDelayMinMs = 0;
        randomDelayMaxMs = 0;
        error = null;
        if (!step.StartsWith("delay ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[6..].Trim();
        if (payload.StartsWith("random ", StringComparison.OrdinalIgnoreCase))
        {
            var randomPayload = payload[7..].Trim();
            var randomParts = randomPayload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int minDelayMs;
            int maxDelayMs;

            if (randomParts.Length is 1 && randomParts[0].Contains("..", StringComparison.Ordinal))
            {
                var range = randomParts[0].Split("..", 2, StringSplitOptions.TrimEntries);
                if (range.Length is not 2
                    || !int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                    || !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
                {
                    error = "Invalid random delay range. Expected: delay random <min> <max> or delay random <min>..<max>.";
                    return true;
                }
            }
            else if (randomParts.Length is not 2
|| !int.TryParse(randomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
|| !int.TryParse(randomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
            {
                error = "Invalid random delay syntax. Expected: delay random <min> <max> or delay random <min>..<max>.";
                return true;
            }

            if (minDelayMs < 0 || maxDelayMs < 0 || minDelayMs > maxDelayMs)
            {
                error = "Invalid random delay bounds. Expected 0 <= min <= max.";
                return true;
            }

            hasRandomDelay = true;
            randomDelayMinMs = minDelayMs;
            randomDelayMaxMs = maxDelayMs;
            return true;
        }

        if (!MacroTiming.TryParseDurationMicroseconds(payload, out fixedDelayMicroseconds))
        {
            error = "Invalid delay value. Expected: delay <ms|us> with a non-negative duration.";
            return true;
        }

        return true;
    }

    private static bool TryParseMove(
        string step,
        out MouseCoordinateMode coordinateMode,
        out MouseCoordinateSpace coordinateSpace,
        out int x,
        out int y,
        out string? error)
    {
        coordinateMode = MouseCoordinateMode.Relative;
        coordinateSpace = MouseCoordinateSpace.LogicalDesktop;
        x = 0;
        y = 0;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!RunScriptSyntax.TryParseMouseMoveMode(parts[1], out coordinateMode, out coordinateSpace))
        {
            error = "Invalid move mode. Expected: abs|absolute|rel|relative|rel-logical|rel-raw.";
            return true;
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            error = "Invalid move coordinates. Expected integers.";
            return true;
        }

        return true;
    }

    private static bool TryParseButton(string step, string command, out MacroMouseButton button, out bool useCurrentPosition, out string? error)
    {
        button = MacroMouseButton.None;
        useCurrentPosition = false;
        error = null;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length is 2)
        {
            if (!TryResolveButton(parts[1], out button))
            {
                error = $"Unknown mouse button '{parts[1]}'.";
                return true;
            }

            return true;
        }

        if (parts.Length is 3 && RunScriptSyntax.IsCurrentPositionToken(parts[1]))
        {
            if (!TryResolveButton(parts[2], out button))
            {
                error = $"Unknown mouse button '{parts[2]}'.";
                return true;
            }

            useCurrentPosition = true;
            return true;
        }

        error = $"Invalid {command} syntax. Expected: {command} <button> or {command} {RunScriptSyntax.CurrentPositionToken} <button>.";
        return true;
    }

    private static bool TryParseKey(string step, out bool isKeyDown, out string keyToken, out string? error)
    {
        isKeyDown = false;
        keyToken = string.Empty;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 3 || !string.Equals(parts[0], "key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(parts[1], "down", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = true;
        }
        else if (string.Equals(parts[1], "up", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = false;
        }
        else
        {
            error = "Invalid key action. Expected: key down <key> | key up <key>.";
            return true;
        }

        keyToken = parts[2];
        return true;
    }

    private static bool TryParseTap(string step, out string combo)
    {
        combo = string.Empty;
        if (!step.StartsWith("tap ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        combo = step[4..].Trim();
        return true;
    }

    private static bool TryParseType(string step, out string text)
    {
        text = string.Empty;
        if (!step.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        text = step[5..];
        return true;
    }

    private static bool TryParseScroll(string step, out MacroMouseButton button, out int count, out string? error)
    {
        button = MacroMouseButton.None;
        count = 1;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3 || !string.Equals(parts[0], "scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        button = parts[1].ToUpperInvariant() switch
        {
            "UP" => MacroMouseButton.ScrollUp,
            "DOWN" => MacroMouseButton.ScrollDown,
            "LEFT" => MacroMouseButton.ScrollLeft,
            "RIGHT" => MacroMouseButton.ScrollRight,
            _ => MacroMouseButton.None,
        };

        if (button is MacroMouseButton.None)
        {
            error = "Unknown scroll direction. Expected: up|down|left|right.";
            return true;
        }

        if (parts.Length is 3
&& (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0))
        {
            error = "Invalid scroll count. Expected integer > 0.";
            return true;
        }

        return true;
    }

    private static bool TryParseScreenReadingStep(string step, out string? error)
    {
        return RunScriptScreenReadingStepParser.TryValidateStep(step, out error);
    }

    private static bool TryResolveButton(string token, out MacroMouseButton button)
    {
        button = token.ToUpperInvariant() switch
        {
            "LEFT" or "L" => MacroMouseButton.Left,
            "RIGHT" or "R" => MacroMouseButton.Right,
            "MIDDLE" or "M" => MacroMouseButton.Middle,
            "SIDE1" or "SIDE" or "BACK" => MacroMouseButton.Side1,
            "SIDE2" or "EXTRA" or "FORWARD" => MacroMouseButton.Side2,
            _ => MacroMouseButton.None,
        };

        return button is not MacroMouseButton.None;
    }

    private sealed class LoopExecutionState
    {
        public int Iterations { get; set; }
    }

    private enum LoopControlSignal
    {
        None = 0,
        Break = 1,
        Continue = 2,
    }

    private sealed class ScriptNodeParseResult
    {
        private ScriptNodeParseResult()
        {
        }

        public bool Success { get; private init; }
        public IReadOnlyList<RunScriptNode>? Nodes { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ScriptNodeParseResult Ok(IReadOnlyList<RunScriptNode> nodes)
        {
            return new ScriptNodeParseResult
            {
                Success = true,
                Nodes = nodes,
            };
        }

        public static ScriptNodeParseResult Fail(string errorMessage)
        {
            return new ScriptNodeParseResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

    private sealed class ScriptExpansionResult
    {
        private ScriptExpansionResult()
        {
        }

        public bool Success { get; private init; }
        public LoopControlSignal LoopControlSignal { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ScriptExpansionResult Ok()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.None,
            };
        }

        public static ScriptExpansionResult Fail(string errorMessage)
        {
            return new ScriptExpansionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }

        public static ScriptExpansionResult Break()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Break,
            };
        }

        public static ScriptExpansionResult Continue()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Continue,
            };
        }
    }

    private sealed class VariableResolutionResult
    {
        private VariableResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public string? Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static VariableResolutionResult Ok(string value)
        {
            return new VariableResolutionResult
            {
                Success = true,
                Value = value,
            };
        }

        public static VariableResolutionResult Fail(string errorMessage)
        {
            return new VariableResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

    private sealed class IntegerResolutionResult
    {
        private IntegerResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public int Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static IntegerResolutionResult Ok(int value)
        {
            return new IntegerResolutionResult
            {
                Success = true,
                Value = value,
            };
        }

        public static IntegerResolutionResult Fail(string errorMessage)
        {
            return new IntegerResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

    private sealed class OperandResolutionResult
    {
        private OperandResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public string? Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static OperandResolutionResult Ok(string value)
        {
            return new OperandResolutionResult
            {
                Success = true,
                Value = value,
            };
        }

        public static OperandResolutionResult Fail(string errorMessage)
        {
            return new OperandResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

    private sealed class NumericConditionOperand
    {
        private NumericConditionOperand()
        {
        }

        public bool Success { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;
        public string? ResolvedValue { get; private init; }
        private int EvaluatedValue { get; init; }
        private bool HasEvaluatedValue { get; init; }

        public static NumericConditionOperand FromEvaluated(int value)
        {
            return new NumericConditionOperand
            {
                Success = true,
                HasEvaluatedValue = true,
                EvaluatedValue = value,
            };
        }

        public static NumericConditionOperand FromResolved(string value)
        {
            return new NumericConditionOperand
            {
                Success = true,
                ResolvedValue = value,
            };
        }

        public static NumericConditionOperand Fail(string errorMessage)
        {
            return new NumericConditionOperand
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }

        public bool TryGetInteger(IReadOnlyDictionary<string, string> variables, out int value)
        {
            if (HasEvaluatedValue)
            {
                value = EvaluatedValue;
                return true;
            }

            return ScriptNumericExpression.TryEvaluate(ResolvedValue!, variables, out value, out _);
        }
    }

    private sealed class ConditionEvaluationResult
    {
        private ConditionEvaluationResult()
        {
        }

        public bool Success { get; private init; }
        public bool Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ConditionEvaluationResult Ok(bool value)
        {
            return new ConditionEvaluationResult
            {
                Success = true,
                Value = value,
            };
        }

        public static ConditionEvaluationResult Fail(string errorMessage)
        {
            return new ConditionEvaluationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }
}
