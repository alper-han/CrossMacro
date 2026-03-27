using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Represents a single script step with optional source metadata.
/// </summary>
public sealed record RunScriptStep(string Step, int? SourceLineNumber = null, int SourceIndex = 0);

/// <summary>
/// Compilation outcome for script steps.
/// </summary>
public sealed class RunScriptCompileResult
{
    private RunScriptCompileResult()
    {
    }

    public bool Success { get; private init; }
    public MacroSequence? Sequence { get; private init; }
    public int InitialDelayMs { get; private init; }
    public bool InitialHasRandomDelay { get; private init; }
    public int InitialRandomDelayMinMs { get; private init; }
    public int InitialRandomDelayMaxMs { get; private init; }
    public string ErrorMessage { get; private init; } = string.Empty;

    public static RunScriptCompileResult Ok(
        MacroSequence sequence,
        int initialDelayMs,
        bool initialHasRandomDelay = false,
        int initialRandomDelayMinMs = 0,
        int initialRandomDelayMaxMs = 0)
    {
        return new RunScriptCompileResult
        {
            Success = true,
            Sequence = sequence,
            InitialDelayMs = initialDelayMs,
            InitialHasRandomDelay = initialHasRandomDelay,
            InitialRandomDelayMinMs = initialRandomDelayMinMs,
            InitialRandomDelayMaxMs = initialRandomDelayMaxMs
        };
    }

    public static RunScriptCompileResult Fail(string errorMessage)
    {
        return new RunScriptCompileResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Compiles run-script style steps (set/repeat/if/while/for + event commands)
/// into executable MacroSequence.
/// </summary>
public sealed class RunScriptCompiler
{
    private const int MaxLoopIterations = 100_000;
    private readonly IKeyCodeMapper _keyCodeMapper;

    public RunScriptCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    }

    public RunScriptCompileResult Compile(IReadOnlyList<RunScriptStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var parseResult = ParseScriptNodes(steps);
        if (!parseResult.Success)
        {
            return RunScriptCompileResult.Fail(parseResult.ErrorMessage);
        }

        var expandedSteps = new List<RunScriptStep>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var loopState = new LoopExecutionState();
        var expandResult = ExpandScriptNodes(parseResult.Nodes!, variables, expandedSteps, loopState, loopDepth: 0);
        if (!expandResult.Success)
        {
            return RunScriptCompileResult.Fail(expandResult.ErrorMessage);
        }

        if (expandResult.LoopControlSignal != LoopControlSignal.None)
        {
            return RunScriptCompileResult.Fail("Internal parser error: unhandled loop-control signal.");
        }

        return CompileExpandedSteps(expandedSteps);
    }

    private RunScriptCompileResult CompileExpandedSteps(IReadOnlyList<RunScriptStep> expandedSteps)
    {
        var sequence = new MacroSequence
        {
            Name = "Run Script",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true
        };

        var timestampMs = 0L;
        var pendingFixedDelayMs = 0;
        var pendingHasRandomDelay = false;
        var pendingRandomDelayMinMs = 0;
        var pendingRandomDelayMaxMs = 0;
        var initialFixedDelayMs = 0;
        var initialHasRandomDelay = false;
        var initialRandomDelayMinMs = 0;
        var initialRandomDelayMaxMs = 0;
        var hasEvents = false;
        bool? moveIsAbsolute = null;
        var hasAbsoluteCursorPosition = false;
        var absoluteCursorX = 0;
        var absoluteCursorY = 0;
        var hasUnpositionedMouseButtonSteps = false;

        for (var i = 0; i < expandedSteps.Count; i++)
        {
            var stepNumber = i + 1;
            var stepEntry = expandedSteps[i];
            var rawStep = stepEntry.Step;
            var lineNumber = stepEntry.SourceLineNumber;
            var stepPrefix = lineNumber.HasValue
                ? $"Step {stepNumber} (line {lineNumber.Value})"
                : $"Step {stepNumber}";

            if (string.IsNullOrWhiteSpace(rawStep))
            {
                return RunScriptCompileResult.Fail($"{stepPrefix}: step cannot be empty.");
            }

            var step = rawStep.Trim();
            var stepForType = rawStep.TrimStart();
            try
            {
                if (TryParseDelay(step, out var hasRandomDelay, out var fixedDelayMs, out var randomDelayMinMs, out var randomDelayMaxMs))
                {
                    if (!hasEvents)
                    {
                        initialFixedDelayMs += fixedDelayMs;
                        if (hasRandomDelay)
                        {
                            initialHasRandomDelay = true;
                            initialRandomDelayMinMs += randomDelayMinMs;
                            initialRandomDelayMaxMs += randomDelayMaxMs;
                        }
                    }
                    else
                    {
                        pendingFixedDelayMs += fixedDelayMs;
                        if (hasRandomDelay)
                        {
                            pendingHasRandomDelay = true;
                            pendingRandomDelayMinMs += randomDelayMinMs;
                            pendingRandomDelayMaxMs += randomDelayMaxMs;
                        }
                    }

                    continue;
                }

                if (TryParseMove(step, out var isAbsolute, out var x, out var y))
                {
                    if (moveIsAbsolute.HasValue && moveIsAbsolute.Value != isAbsolute)
                    {
                        return RunScriptCompileResult.Fail($"{stepPrefix}: cannot mix absolute and relative move modes in a single run script.");
                    }

                    if (isAbsolute && hasUnpositionedMouseButtonSteps)
                    {
                        return RunScriptCompileResult.Fail(
                            $"{stepPrefix}: absolute mode cannot be introduced after click/down/up steps. Place 'move abs <x> <y>' before mouse button steps.");
                    }

                    moveIsAbsolute ??= isAbsolute;
                    EmitEvent(new MacroEvent
                    {
                        Type = EventType.MouseMove,
                        X = x,
                        Y = y
                    });

                    if (isAbsolute)
                    {
                        hasAbsoluteCursorPosition = true;
                        absoluteCursorX = x;
                        absoluteCursorY = y;
                    }

                    continue;
                }

                if (TryParseButton(step, "down", out var downButton, out var isCurrentPositionDown))
                {
                    var downEvent = new MacroEvent
                    {
                        Type = EventType.ButtonPress,
                        Button = downButton,
                        UseCurrentPosition = isCurrentPositionDown || moveIsAbsolute == null
                    };

                    if (isCurrentPositionDown)
                    {
                        EmitEvent(downEvent);
                        continue;
                    }

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunScriptCompileResult.Fail(
                                $"{stepPrefix}: down <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        downEvent.X = absoluteCursorX;
                        downEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(downEvent);
                    continue;
                }

                if (TryParseButton(step, "up", out var upButton, out var isCurrentPositionUp))
                {
                    var upEvent = new MacroEvent
                    {
                        Type = EventType.ButtonRelease,
                        Button = upButton,
                        UseCurrentPosition = isCurrentPositionUp || moveIsAbsolute == null
                    };

                    if (isCurrentPositionUp)
                    {
                        EmitEvent(upEvent);
                        continue;
                    }

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunScriptCompileResult.Fail(
                                $"{stepPrefix}: up <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        upEvent.X = absoluteCursorX;
                        upEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(upEvent);
                    continue;
                }

                if (TryParseButton(step, "click", out var clickButton, out var isCurrentPositionClick))
                {
                    var clickEvent = new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = clickButton,
                        UseCurrentPosition = isCurrentPositionClick || moveIsAbsolute == null
                    };

                    if (isCurrentPositionClick)
                    {
                        EmitEvent(clickEvent);
                        continue;
                    }

                    if (moveIsAbsolute == true)
                    {
                        if (!hasAbsoluteCursorPosition)
                        {
                            return RunScriptCompileResult.Fail(
                                $"{stepPrefix}: click <button> requires a prior 'move abs <x> <y>' step in absolute mode.");
                        }

                        clickEvent.X = absoluteCursorX;
                        clickEvent.Y = absoluteCursorY;
                    }
                    else if (moveIsAbsolute == null)
                    {
                        hasUnpositionedMouseButtonSteps = true;
                    }

                    EmitEvent(clickEvent);
                    continue;
                }

                if (TryParseScroll(step, out var scrollButton, out var scrollCount, out var scrollError))
                {
                    if (scrollError != null)
                    {
                        return RunScriptCompileResult.Fail($"{stepPrefix}: {scrollError}");
                    }

                    for (var c = 0; c < scrollCount; c++)
                    {
                        EmitEvent(new MacroEvent
                        {
                            Type = EventType.Click,
                            Button = scrollButton
                        });
                    }

                    continue;
                }

                if (TryParseKey(step, out var isKeyDown, out var keyToken))
                {
                    var keyCode = ResolveKeyCode(keyToken);
                    if (keyCode < 0)
                    {
                        return RunScriptCompileResult.Fail($"{stepPrefix}: unknown key '{keyToken}'.");
                    }

                    EmitEvent(new MacroEvent
                    {
                        Type = isKeyDown ? EventType.KeyPress : EventType.KeyRelease,
                        KeyCode = keyCode
                    });

                    continue;
                }

                if (TryParseTap(step, out var combo))
                {
                    var comboParts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (comboParts.Length == 0)
                    {
                        return RunScriptCompileResult.Fail($"{stepPrefix}: tap combo cannot be empty.");
                    }

                    var modifiers = new List<int>();
                    var primaryKeys = new List<int>();
                    foreach (var part in comboParts)
                    {
                        var code = ResolveKeyCode(part);
                        if (code < 0)
                        {
                            return RunScriptCompileResult.Fail($"{stepPrefix}: unknown key '{part}' in tap combo.");
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
                    if (primaryKeys.Count == 0 && distinctModifiers.Count == 1)
                    {
                        EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = distinctModifiers[0] });
                        EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = distinctModifiers[0] });
                        continue;
                    }

                    if (primaryKeys.Count != 1)
                    {
                        return RunScriptCompileResult.Fail(
                            $"{stepPrefix}: tap expects either exactly one non-modifier key (example: ctrl+c) or a single modifier key.");
                    }

                    foreach (var modifier in distinctModifiers)
                    {
                        EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
                    }

                    EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = primaryKeys[0] });
                    EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = primaryKeys[0] });

                    for (var m = distinctModifiers.Count - 1; m >= 0; m--)
                    {
                        EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = distinctModifiers[m] });
                    }

                    continue;
                }

                if (TryParseType(stepForType, out var textToType))
                {
                    if (textToType.Length == 0)
                    {
                        return RunScriptCompileResult.Fail($"{stepPrefix}: type text cannot be empty.");
                    }

                    foreach (var ch in textToType)
                    {
                        if (ch == '\r')
                        {
                            continue;
                        }

                        if (ch == '\n')
                        {
                            EmitTapKeyByName("Enter");
                            continue;
                        }

                        if (ch == '\t')
                        {
                            EmitTapKeyByName("Tab");
                            continue;
                        }

                        var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(ch);
                        if (keyCode < 0)
                        {
                            return RunScriptCompileResult.Fail($"{stepPrefix}: cannot map character '{ch}' for type command.");
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
                                return RunScriptCompileResult.Fail($"{stepPrefix}: required modifier key is not available for type command.");
                            }

                            EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = modifier });
                        }

                        EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = keyCode });
                        EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = keyCode });

                        for (var m = modifiers.Count - 1; m >= 0; m--)
                        {
                            EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = modifiers[m] });
                        }
                    }

                    continue;
                }

                return RunScriptCompileResult.Fail($"{stepPrefix}: unsupported step syntax '{rawStep}'.");
            }
            catch (ArgumentException ex)
            {
                return RunScriptCompileResult.Fail($"{stepPrefix}: {ex.Message}");
            }
        }

        if (!hasEvents)
        {
            return RunScriptCompileResult.Fail(
                "Run script did not produce any executable events. Add at least one runtime step (move/click/down/up/scroll/key/tap/type).");
        }

        sequence.IsAbsoluteCoordinates = moveIsAbsolute ?? false;
        sequence.TrailingDelayMs = pendingFixedDelayMs;
        sequence.HasTrailingRandomDelay = pendingHasRandomDelay;
        sequence.TrailingDelayMinMs = pendingRandomDelayMinMs;
        sequence.TrailingDelayMaxMs = pendingRandomDelayMaxMs;
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type == EventType.Click || e.Type == EventType.ButtonPress || e.Type == EventType.ButtonRelease);
        sequence.CalculateDuration();

        return RunScriptCompileResult.Ok(
            sequence,
            initialFixedDelayMs,
            initialHasRandomDelay,
            initialRandomDelayMinMs,
            initialRandomDelayMaxMs);

        void EmitEvent(MacroEvent ev)
        {
            ev.DelayMs = pendingFixedDelayMs;
            ev.HasRandomDelay = pendingHasRandomDelay;
            ev.RandomDelayMinMs = pendingRandomDelayMinMs;
            ev.RandomDelayMaxMs = pendingRandomDelayMaxMs;
            timestampMs += pendingFixedDelayMs;
            if (pendingHasRandomDelay)
            {
                timestampMs += pendingRandomDelayMinMs;
            }

            ev.Timestamp = timestampMs;
            pendingFixedDelayMs = 0;
            pendingHasRandomDelay = false;
            pendingRandomDelayMinMs = 0;
            pendingRandomDelayMaxMs = 0;
            sequence.Events.Add(ev);
            hasEvents = true;
        }

        void EmitTapKeyByName(string keyName)
        {
            var code = ResolveKeyCode(keyName);
            if (code < 0)
            {
                throw new ArgumentException($"Unknown key '{keyName}'.");
            }

            EmitEvent(new MacroEvent { Type = EventType.KeyPress, KeyCode = code });
            EmitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = code });
        }
    }

    private ScriptNodeParseResult ParseScriptNodes(IReadOnlyList<RunScriptStep> steps)
    {
        var index = 0;
        var result = ParseBlockNodes(steps, ref index, isTopLevel: true);
        if (!result.Success)
        {
            return result;
        }

        return ScriptNodeParseResult.Ok(result.Nodes!);
    }

    private ScriptNodeParseResult ParseBlockNodes(IReadOnlyList<RunScriptStep> steps, ref int index, bool isTopLevel)
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

            if (TryParseRepeatHeader(trimmed, out var repeatCountToken))
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
                if (ifHeaderError != null)
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
                if (whileHeaderError != null)
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

            if (TryParseForHeader(trimmed, out var forHeader, out var forHeaderError))
            {
                if (forHeaderError != null)
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

            if (trimmed.EndsWith("{", StringComparison.Ordinal))
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

    private ScriptExpansionResult ExpandScriptNodes(
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
                        if (loopDepth == 0)
                        {
                            return ScriptExpansionResult.Fail($"{source}: 'break' can only be used inside repeat/while/for blocks.");
                        }

                        return ScriptExpansionResult.Break();
                    }

                    if (RunScriptSyntax.IsContinueCommand(step))
                    {
                        if (loopDepth == 0)
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

                        if (TryEvaluateNumericExpression(resolvedValueResult.Value!, out var numericValue, out var expressionError))
                        {
                            if (expressionError != null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {expressionError}");
                            }

                            variables[variableName!] = numericValue.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            variables[variableName!] = resolvedValueResult.Value!;
                        }

                        break;
                    }

                    if (TryParseIncDecCommand(step, out var targetVariableName, out var amountToken, out var sign, out var incDecError))
                    {
                        if (incDecError != null)
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

                        var updated = existingInt + sign * amountResult.Value;
                        variables[targetVariableName!] = updated.ToString(CultureInfo.InvariantCulture);
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
                    var repeatCountResult = ResolveIntegerToken(repeat.CountToken, variables, "repeat count");
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

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Break)
                        {
                            break;
                        }

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Continue)
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
                    if (branch == null || branch.Count == 0)
                    {
                        break;
                    }

                    var nestedResult = ExpandScriptNodes(branch, variables, output, loopState, loopDepth);
                    if (!nestedResult.Success)
                    {
                        return nestedResult;
                    }

                    if (nestedResult.LoopControlSignal != LoopControlSignal.None)
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

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Break)
                        {
                            break;
                        }

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Continue)
                        {
                            continue;
                        }
                    }

                    break;
                }
                case ForNode forNode:
                {
                    var source = BuildSourcePrefix(forNode.Source);
                    var startResult = ResolveIntegerToken(forNode.StartToken, variables, "for start");
                    if (!startResult.Success)
                    {
                        return ScriptExpansionResult.Fail($"{source}: {startResult.ErrorMessage}");
                    }

                    var endResult = ResolveIntegerToken(forNode.EndToken, variables, "for end");
                    if (!endResult.Success)
                    {
                        return ScriptExpansionResult.Fail($"{source}: {endResult.ErrorMessage}");
                    }

                    int stepValue;
                    if (forNode.HasExplicitStep)
                    {
                        var stepResult = ResolveIntegerToken(forNode.StepToken!, variables, "for step");
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

                    if (stepValue == 0)
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

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Break)
                        {
                            break;
                        }

                        if (nestedResult.LoopControlSignal == LoopControlSignal.Continue)
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

    private static bool TryParseRepeatHeader(string step, out string countToken)
    {
        countToken = string.Empty;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3
            && string.Equals(parts[0], "repeat", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[2], "{", StringComparison.Ordinal))
        {
            countToken = parts[1];
            return true;
        }

        return false;
    }

    private static bool TryParseIfHeader(string step, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        error = null;

        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
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

        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith("while ", StringComparison.OrdinalIgnoreCase))
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

    private static bool TryParseForHeader(string step, out ForHeader? header, out string? error)
    {
        header = null;
        error = null;

        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[..^1].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 6 && parts.Length != 8)
        {
            error = "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {";
            return true;
        }

        if (!string.Equals(parts[2], "from", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], "to", StringComparison.OrdinalIgnoreCase))
        {
            error = "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {";
            return true;
        }

        var variableName = parts[1];
        if (!IsValidVariableName(variableName))
        {
            error = $"Invalid loop variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        var startToken = parts[3];
        var endToken = parts[5];
        string? stepToken = null;
        var hasExplicitStep = false;

        if (parts.Length == 8)
        {
            if (!string.Equals(parts[6], "step", StringComparison.OrdinalIgnoreCase))
            {
                error = "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {";
                return true;
            }

            stepToken = parts[7];
            hasExplicitStep = true;
        }

        header = new ForHeader(variableName, startToken, endToken, stepToken, hasExplicitStep);
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
        var leftResult = ResolveOperandValue(condition.LeftToken, variables);
        if (!leftResult.Success)
        {
            return ConditionEvaluationResult.Fail(leftResult.ErrorMessage);
        }

        var rightResult = ResolveOperandValue(condition.RightToken, variables);
        if (!rightResult.Success)
        {
            return ConditionEvaluationResult.Fail(rightResult.ErrorMessage);
        }

        var leftValue = leftResult.Value!;
        var rightValue = rightResult.Value!;

        if (condition.OperatorToken is "==" or "!=")
        {
            var equals = ValuesEqual(leftValue, rightValue);
            return ConditionEvaluationResult.Ok(condition.OperatorToken == "==" ? equals : !equals);
        }

        if (!int.TryParse(leftValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftInt)
            || !int.TryParse(rightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightInt))
        {
            return ConditionEvaluationResult.Fail(
                $"Operator '{condition.OperatorToken}' requires numeric operands. Got '{leftValue}' and '{rightValue}'.");
        }

        var result = condition.OperatorToken switch
        {
            ">" => leftInt > rightInt,
            ">=" => leftInt >= rightInt,
            "<" => leftInt < rightInt,
            "<=" => leftInt <= rightInt,
            _ => false
        };

        return ConditionEvaluationResult.Ok(result);
    }

    private static bool ValuesEqual(string left, string right)
    {
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
            var escapedLiteral = UnescapeLiteralDollar(token);
            return OperandResolutionResult.Ok(Unquote(escapedLiteral));
        }

        var value = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!IsValidVariableName(variableName))
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
        value = UnescapeLiteralDollar(value);
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

    private static string UnescapeLiteralDollar(string input)
    {
        return input.Replace("$$", "$", StringComparison.Ordinal);
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
        if (payload.Length == 0)
        {
            error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
            return true;
        }

        var equalIndex = payload.IndexOf('=');
        if (equalIndex >= 0)
        {
            variableName = payload[..equalIndex].Trim();
            variableValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
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

        if (!IsValidVariableName(variableName))
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
        var command = sign == 1 ? "inc" : "dec";
        var payload = step[4..].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2)
        {
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        variableName = parts[0];
        amountToken = parts.Length == 2 ? parts[1] : "1";
        if (!IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        return true;
    }

    private static bool TryEvaluateNumericExpression(string expression, out int value, out string? error)
    {
        value = 0;
        error = null;
        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        var operatorIndex = FindBinaryOperatorIndex(trimmed);
        if (operatorIndex <= 0)
        {
            return false;
        }

        var op = trimmed[operatorIndex];
        var leftToken = trimmed[..operatorIndex].Trim();
        var rightToken = trimmed[(operatorIndex + 1)..].Trim();
        if (!int.TryParse(leftToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
            || !int.TryParse(rightToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right))
        {
            return false;
        }

        switch (op)
        {
            case '+':
                value = left + right;
                return true;
            case '-':
                value = left - right;
                return true;
            case '*':
                value = left * right;
                return true;
            case '/':
                if (right == 0)
                {
                    error = "Division by zero is not allowed in set expressions.";
                    return true;
                }

                value = left / right;
                return true;
            case '%':
                if (right == 0)
                {
                    error = "Modulo by zero is not allowed in set expressions.";
                    return true;
                }

                value = left % right;
                return true;
            default:
                return false;
        }
    }

    private static int FindBinaryOperatorIndex(string expression)
    {
        for (var i = 1; i < expression.Length - 1; i++)
        {
            var ch = expression[i];
            if (ch is '+' or '-' or '*' or '/' or '%')
            {
                return i;
            }
        }

        return -1;
    }

    private static VariableResolutionResult ResolveVariables(string input, IReadOnlyDictionary<string, string> variables)
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
                output.Append(ch);
                continue;
            }

            if (i + 1 >= input.Length)
            {
                return VariableResolutionResult.Fail("Invalid variable reference '$'.");
            }

            var next = input[i + 1];
            if (next == '$')
            {
                output.Append('$');
                i++;
                continue;
            }

            if (!IsVariableNameStart(next))
            {
                return VariableResolutionResult.Fail($"Invalid variable reference '${next}'.");
            }

            var j = i + 1;
            while (j < input.Length && IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            if (!variables.TryGetValue(variableName, out var value))
            {
                return VariableResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }

            output.Append(value);
            i = j - 1;
        }

        return VariableResolutionResult.Ok(output.ToString());
    }

    private static IntegerResolutionResult ResolveIntegerToken(
        string token,
        IReadOnlyDictionary<string, string> variables,
        string description)
    {
        var resolved = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!IsValidVariableName(variableName))
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

    private static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!IsVariableNameStart(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!IsVariableNamePart(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVariableNameStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    private static bool IsVariableNamePart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }

    private static string BuildSourcePrefix(RunScriptStep entry)
    {
        var index = entry.SourceIndex > 0 ? entry.SourceIndex : 1;
        return entry.SourceLineNumber.HasValue
            ? $"Step {index} (line {entry.SourceLineNumber.Value})"
            : $"Step {index}";
    }

    private int ResolveKeyCode(string keyToken)
    {
        if (int.TryParse(keyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCode))
        {
            return parsedCode;
        }

        return _keyCodeMapper.GetKeyCode(keyToken);
    }

    private static bool TryParseDelay(
        string step,
        out bool hasRandomDelay,
        out int fixedDelayMs,
        out int randomDelayMinMs,
        out int randomDelayMaxMs)
    {
        hasRandomDelay = false;
        fixedDelayMs = 0;
        randomDelayMinMs = 0;
        randomDelayMaxMs = 0;
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

            if (randomParts.Length == 1 && randomParts[0].Contains("..", StringComparison.Ordinal))
            {
                var range = randomParts[0].Split("..", 2, StringSplitOptions.TrimEntries);
                if (range.Length != 2
                    || !int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                    || !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
                {
                    throw new ArgumentException("Invalid random delay range. Expected: delay random <min> <max> or delay random <min>..<max>.");
                }
            }
            else if (randomParts.Length == 2
                     && int.TryParse(randomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                     && int.TryParse(randomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
            {
            }
            else
            {
                throw new ArgumentException("Invalid random delay syntax. Expected: delay random <min> <max> or delay random <min>..<max>.");
            }

            if (minDelayMs < 0 || maxDelayMs < 0 || minDelayMs > maxDelayMs)
            {
                throw new ArgumentException("Invalid random delay bounds. Expected 0 <= min <= max.");
            }

            hasRandomDelay = true;
            randomDelayMinMs = minDelayMs;
            randomDelayMaxMs = maxDelayMs;
            return true;
        }

        if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out fixedDelayMs) || fixedDelayMs < 0)
        {
            throw new ArgumentException("Invalid delay value. Expected: delay <ms> with ms >= 0.");
        }

        return true;
    }

    private static bool TryParseMove(string step, out bool isAbsolute, out int x, out int y)
    {
        isAbsolute = false;
        x = 0;
        y = 0;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(parts[1], "abs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parts[1], "absolute", StringComparison.OrdinalIgnoreCase))
        {
            isAbsolute = true;
        }
        else if (string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(parts[1], "relative", StringComparison.OrdinalIgnoreCase))
        {
            isAbsolute = false;
        }
        else
        {
            throw new ArgumentException("Invalid move mode. Expected: abs|absolute|rel|relative.");
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            throw new ArgumentException("Invalid move coordinates. Expected integers.");
        }

        return true;
    }

    private static bool TryParseButton(string step, string command, out MouseButton button, out bool useCurrentPosition)
    {
        button = MouseButton.None;
        useCurrentPosition = false;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (!TryResolveButton(parts[1], out button))
            {
                throw new ArgumentException($"Unknown mouse button '{parts[1]}'.");
            }

            return true;
        }

        if (parts.Length == 3 && RunScriptSyntax.IsCurrentPositionToken(parts[1]))
        {
            if (!TryResolveButton(parts[2], out button))
            {
                throw new ArgumentException($"Unknown mouse button '{parts[2]}'.");
            }

            useCurrentPosition = true;
            return true;
        }

        throw new ArgumentException(
            $"Invalid {command} syntax. Expected: {command} <button> or {command} {RunScriptSyntax.CurrentPositionToken} <button>.");
    }

    private static bool TryParseKey(string step, out bool isKeyDown, out string keyToken)
    {
        isKeyDown = false;
        keyToken = string.Empty;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "key", StringComparison.OrdinalIgnoreCase))
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
            throw new ArgumentException("Invalid key action. Expected: key down <key> | key up <key>.");
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

    private static bool TryParseScroll(string step, out MouseButton button, out int count, out string? error)
    {
        button = MouseButton.None;
        count = 1;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3 || !string.Equals(parts[0], "scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        button = parts[1].ToLowerInvariant() switch
        {
            "up" => MouseButton.ScrollUp,
            "down" => MouseButton.ScrollDown,
            "left" => MouseButton.ScrollLeft,
            "right" => MouseButton.ScrollRight,
            _ => MouseButton.None
        };

        if (button == MouseButton.None)
        {
            error = "Unknown scroll direction. Expected: up|down|left|right.";
            return true;
        }

        if (parts.Length == 3
            && (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0))
        {
            error = "Invalid scroll count. Expected integer > 0.";
            return true;
        }

        return true;
    }

    private static bool TryResolveButton(string token, out MouseButton button)
    {
        button = token.ToLowerInvariant() switch
        {
            "left" or "l" => MouseButton.Left,
            "right" or "r" => MouseButton.Right,
            "middle" or "m" => MouseButton.Middle,
            "side1" or "side" or "back" => MouseButton.Side1,
            "side2" or "extra" or "forward" => MouseButton.Side2,
            _ => MouseButton.None
        };

        return button != MouseButton.None;
    }

    private sealed class LoopExecutionState
    {
        public int Iterations { get; set; }
    }

    private enum LoopControlSignal
    {
        None = 0,
        Break = 1,
        Continue = 2
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
                Nodes = nodes
            };
        }

        public static ScriptNodeParseResult Fail(string errorMessage)
        {
            return new ScriptNodeParseResult
            {
                Success = false,
                ErrorMessage = errorMessage
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
                LoopControlSignal = LoopControlSignal.None
            };
        }

        public static ScriptExpansionResult Fail(string errorMessage)
        {
            return new ScriptExpansionResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        public static ScriptExpansionResult Break()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Break
            };
        }

        public static ScriptExpansionResult Continue()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Continue
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
                Value = value
            };
        }

        public static VariableResolutionResult Fail(string errorMessage)
        {
            return new VariableResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage
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
                Value = value
            };
        }

        public static IntegerResolutionResult Fail(string errorMessage)
        {
            return new IntegerResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage
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
                Value = value
            };
        }

        public static OperandResolutionResult Fail(string errorMessage)
        {
            return new OperandResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
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
                Value = value
            };
        }

        public static ConditionEvaluationResult Fail(string errorMessage)
        {
            return new ConditionEvaluationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed record ConditionExpression(string LeftToken, string OperatorToken, string RightToken);

    private sealed record ForHeader(
        string VariableName,
        string StartToken,
        string EndToken,
        string? StepToken,
        bool HasExplicitStep);

    private abstract record RunScriptNode(RunScriptStep Source);
    private sealed record CommandNode(RunScriptStep Source) : RunScriptNode(Source);
    private sealed record RepeatNode(RunScriptStep Source, string CountToken, IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
    private sealed record IfNode(
        RunScriptStep Source,
        ConditionExpression Condition,
        IReadOnlyList<RunScriptNode> TrueBody,
        RunScriptStep? ElseSource,
        IReadOnlyList<RunScriptNode>? FalseBody) : RunScriptNode(Source);
    private sealed record WhileNode(
        RunScriptStep Source,
        ConditionExpression Condition,
        IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
    private sealed record ForNode(
        RunScriptStep Source,
        string VariableName,
        string StartToken,
        string EndToken,
        string? StepToken,
        bool HasExplicitStep,
        IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
}
