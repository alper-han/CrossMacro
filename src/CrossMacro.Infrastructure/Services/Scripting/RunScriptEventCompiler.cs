namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Compiles fully expanded steps (no control flow, no variables) into a <see cref="MacroSequence"/> of input
/// events. Screen-reading steps cannot be compiled to events; they are validated here and make the sequence
/// carry its script steps for runtime execution.
/// </summary>
internal sealed class RunScriptEventCompiler(IKeyCodeMapper keyCodeMapper)
{
    private readonly IKeyCodeMapper _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));

    public RunScriptCompileResult Compile(IReadOnlyList<RunScriptStep> expandedSteps)
    {
        ArgumentNullException.ThrowIfNull(expandedSteps);

        var compilation = new Compilation(_keyCodeMapper);
        for (var i = 0; i < expandedSteps.Count; i++)
        {
            var entry = expandedSteps[i];
            var prefix = DescribeStep(i + 1, entry.SourceLineNumber);
            if (string.IsNullOrWhiteSpace(entry.Step))
            {
                return RunScriptCompileResult.Fail($"{prefix}: step cannot be empty.");
            }

            var error = compilation.CompileStep(entry.Step);
            if (error is not null)
            {
                return RunScriptCompileResult.Fail($"{prefix}: {error}");
            }
        }

        return compilation.Complete(expandedSteps);
    }

    private static string DescribeStep(int stepNumber, int? lineNumber) => lineNumber is { } line
        ? $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)} (line {line.ToString(CultureInfo.InvariantCulture)})"
        : $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}";

    private sealed class Compilation
    {
        private readonly RunScriptEventTimeline _timeline = new();
        private readonly RunScriptMouseEmitter _mouse;
        private readonly RunScriptKeyboardEmitter _keyboard;
        private bool _hasScreenReadingSteps;

        public Compilation(IKeyCodeMapper keyCodeMapper)
        {
            _mouse = new RunScriptMouseEmitter(_timeline);
            _keyboard = new RunScriptKeyboardEmitter(keyCodeMapper, _timeline);
        }

        /// <summary>Compiles one step; returns the problem, or null when the step was accepted.</summary>
        public string? CompileStep(string rawStep)
        {
            var step = rawStep.Trim();
            string? error;
            if (TryAddDelay(step, out error)
                || _mouse.TryEmitMove(step, out error)
                || _mouse.TryEmitButton(step, out error)
                || _mouse.TryEmitScroll(step, out error)
                || TryAcceptScreenReading(step, out error)
                || _keyboard.TryEmitKey(step, out error)
                || _keyboard.TryEmitTap(step, out error)
                || _keyboard.TryEmitType(rawStep.TrimStart(), out error))
            {
                return error;
            }

            return $"unsupported step syntax '{rawStep}'.";
        }

        public RunScriptCompileResult Complete(IReadOnlyList<RunScriptStep> expandedSteps)
        {
            if (!_timeline.HasEvents && !_hasScreenReadingSteps)
            {
                return RunScriptCompileResult.Fail(
                    "Run script did not produce any executable events. Add at least one runtime step (move/click/down/up/scroll/key/tap/type).");
            }

            var sequence = _timeline.Sequence;
            if (_hasScreenReadingSteps)
            {
                sequence.ReplaceScriptSteps(expandedSteps
                    .Select(step => step.Step.Trim())
                    .Where(step => !string.IsNullOrWhiteSpace(step))
                    .ToList());
            }

            var trailing = _timeline.Trailing;
            sequence.IsAbsoluteCoordinates = MacroPositionSemantics.GetCoordinateModeSummary(sequence) is CoordinateModeSummary.Absolute;
            sequence.TrailingDelayMicroseconds = trailing.FixedMicroseconds;
            sequence.HasTrailingRandomDelay = trailing.HasRandom;
            sequence.TrailingDelayMinMs = trailing.RandomMinMs;
            sequence.TrailingDelayMaxMs = trailing.RandomMaxMs;
            sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
            sequence.ClickCount = sequence.Events.Count(e => e.Type is EventType.Click or EventType.ButtonPress or EventType.ButtonRelease);
            sequence.CalculateDuration();

            var initial = _timeline.Initial;
            return RunScriptCompileResult.Ok(
                sequence,
                initial.FixedMicroseconds,
                initial.HasRandom,
                initial.RandomMinMs,
                initial.RandomMaxMs);
        }

        private bool TryAddDelay(string step, out string? error)
        {
            if (!RunScriptInputSyntax.TryParseDelay(step, out var hasRandom, out var fixedMicroseconds, out var randomMinMs, out var randomMaxMs, out error))
            {
                return false;
            }

            if (error is null)
            {
                _timeline.AddDelay(fixedMicroseconds, hasRandom, randomMinMs, randomMaxMs);
            }

            return true;
        }

        private bool TryAcceptScreenReading(string step, out string? error)
        {
            if (!RunScriptScreenReadingStepParser.TryValidateStep(step, out error))
            {
                return false;
            }

            _hasScreenReadingSteps |= error is null;
            return true;
        }
    }
}
