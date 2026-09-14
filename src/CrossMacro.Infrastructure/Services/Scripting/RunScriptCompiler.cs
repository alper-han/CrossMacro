
namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Compiles run-script style steps (set/repeat/if/while/for + event commands)
/// into executable MacroSequence.
/// </summary>
public sealed class RunScriptCompiler
{
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

        var parseResult = RunScriptTreeReader.ParseScriptNodes(steps);
        if (!parseResult.Success)
        {
            return RunScriptCompileResult.Fail(parseResult.ErrorMessage);
        }

        if (RunScriptRuntimeValidator.ContainsRuntimeServiceNode(parseResult.Nodes!))
        {
            return CompileRuntimeScriptBackedSteps(steps, parseResult.Nodes!);
        }

        var expansion = RunScriptStaticExpander.Expand(parseResult.Nodes!);
        if (!expansion.Success)
        {
            return RunScriptCompileResult.Fail(expansion.ErrorMessage);
        }
        var expandedSteps = expansion.Steps;

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
            if (RunScriptInputSyntax.TryParseDelay(step, out var hasRandomDelay, out var fixedDelayMicroseconds, out var randomDelayMinMs, out var randomDelayMaxMs, out var delayError))
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

            if (RunScriptInputSyntax.TryParseMove(
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

            if (RunScriptInputSyntax.TryParseScroll(step, out var scrollButton, out var scrollCount, out var scrollError))
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

            if (RunScriptInputSyntax.TryParseKey(step, out var isKeyDown, out var keyToken, out var keyError))
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
            ev.DelayMicroseconds = checked(ev.DelayMicroseconds + pendingFixedDelayMicroseconds);
            ev.HasRandomDelay = pendingHasRandomDelay;
            ev.RandomDelayMinMs = pendingRandomDelayMinMs;
            ev.RandomDelayMaxMs = pendingRandomDelayMaxMs;
            timestampMicroseconds += ev.DelayMicroseconds;
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
            if (!RunScriptInputSyntax.TryParseButton(stepToParse, command, out var button, out var useCurrentPosition, out var buttonError))
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
        if (!RunScriptInputSyntax.TryParseTap(step, out var combo))
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
        if (!RunScriptInputSyntax.TryParseType(stepForType, out var textToType))
        {
            return false;
        }

        if (textToType.Length is 0)
        {
            error = $"{stepPrefix}: type text cannot be empty.";
            return true;
        }

        var isFirstCharacter = true;
        for (var index = 0; index < textToType.Length; index++)
        {
            var ch = textToType[index];
            if (ch == '\r')
            {
                if (index + 1 < textToType.Length && textToType[index + 1] == '\n')
                {
                    index++;
                }

                if (!TryEmitTapKeyByName("Enter", isFirstCharacter, emitEvent, out var carriageReturnError))
                {
                    error = $"{stepPrefix}: {carriageReturnError}";
                    return true;
                }

                isFirstCharacter = false;
                continue;
            }

            if (ch == '\n')
            {
                if (!TryEmitTapKeyByName("Enter", isFirstCharacter, emitEvent, out var lineFeedError))
                {
                    error = $"{stepPrefix}: {lineFeedError}";
                    return true;
                }

                isFirstCharacter = false;
                continue;
            }

            if (ch == '\t')
            {
                if (!TryEmitTapKeyByName("Tab", isFirstCharacter, emitEvent, out var tabError))
                {
                    error = $"{stepPrefix}: {tabError}";
                    return true;
                }

                isFirstCharacter = false;
                continue;
            }

            if (ch == '\b')
            {
                if (!TryEmitTapKeyByName("Backspace", isFirstCharacter, emitEvent, out var backspaceError))
                {
                    error = $"{stepPrefix}: {backspaceError}";
                    return true;
                }

                isFirstCharacter = false;
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

            emitEvent(new MacroEvent
            {
                Type = EventType.KeyPress,
                KeyCode = keyCode,
                DelayMicroseconds = isFirstCharacter
                    ? 0
                    : MacroTiming.DefaultKeyPressDelayMicroseconds,
            });
            emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = keyCode });

            for (var m = modifiers.Count - 1; m >= 0; m--)
            {
                emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = modifiers[m] });
            }

            isFirstCharacter = false;
        }

        return true;
    }

    private bool TryEmitTapKeyByName(
        string keyName,
        bool isFirstCharacter,
        Action<MacroEvent> emitEvent,
        out string? error)
    {
        var code = ResolveKeyCode(keyName);
        if (code < 0)
        {
            error = $"Unknown key '{keyName}'.";
            return false;
        }

        emitEvent(new MacroEvent
        {
            Type = EventType.KeyPress,
            KeyCode = code,
            DelayMicroseconds = isFirstCharacter
                ? 0
                : MacroTiming.DefaultKeyPressDelayMicroseconds,
        });
        emitEvent(new MacroEvent { Type = EventType.KeyRelease, KeyCode = code });
        error = null;
        return true;
    }

    private static bool TryParseScreenReadingStep(string step, out string? error)
    {
        return RunScriptScreenReadingStepParser.TryValidateStep(step, out error);
    }

}
