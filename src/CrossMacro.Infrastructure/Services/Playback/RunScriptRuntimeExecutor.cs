
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptRuntimeExecutor(
    IKeyCodeMapper keyCodeMapper,
    IPlaybackTimingService timingService,
    IPlaybackPauseToken pauseToken,
    IDictionary<string, string> runtimeVariables,
    RunScriptScreenReadExecutor screenReadExecutor,
    RunScriptWindowExecutor windowExecutor,
    RunScriptClipboardExecutor clipboardExecutor,
    RunScriptShellExecutor shellExecutor,
    RunScriptScreenshotExecutor screenshotExecutor,
    RunScriptMousePositionExecutor mousePositionExecutor)
{

    private enum LoopControlSignal
    {
        None,
        Break,
        Continue,
    }

    private sealed class LoopExecutionState
    {
        public int Iterations { get; private set; }

        public void Advance()
        {
            if (++Iterations > ScriptExecutionLimits.RuntimeExecutionIterations)
            {
                throw new InvalidOperationException($"Runtime loop iteration limit exceeded ({ScriptExecutionLimits.RuntimeExecutionIterations}). Check loop exit condition.");
            }
        }
    }

    private readonly IKeyCodeMapper _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    private readonly IPlaybackTimingService _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
    private readonly IPlaybackPauseToken _pauseToken = pauseToken ?? throw new ArgumentNullException(nameof(pauseToken));
    private readonly IDictionary<string, string> _runtimeVariables = runtimeVariables ?? throw new ArgumentNullException(nameof(runtimeVariables));

    // Read-only live view for the Core evaluator (takes IReadOnlyDictionary).
    private readonly IReadOnlyDictionary<string, string> _runtimeVariablesView =
        new ReadOnlyDictionary<string, string>(runtimeVariables ?? throw new ArgumentNullException(nameof(runtimeVariables)));
    private readonly RunScriptScreenReadExecutor _screenReadExecutor = screenReadExecutor ?? throw new ArgumentNullException(nameof(screenReadExecutor));
    private readonly RunScriptWindowExecutor _windowExecutor = windowExecutor ?? throw new ArgumentNullException(nameof(windowExecutor));
    private readonly RunScriptClipboardExecutor _clipboardExecutor = clipboardExecutor ?? throw new ArgumentNullException(nameof(clipboardExecutor));
    private readonly RunScriptShellExecutor _shellExecutor = shellExecutor ?? throw new ArgumentNullException(nameof(shellExecutor));
    private readonly RunScriptScreenshotExecutor _screenshotExecutor = screenshotExecutor ?? throw new ArgumentNullException(nameof(screenshotExecutor));
    private readonly RunScriptMousePositionExecutor _mousePositionExecutor = mousePositionExecutor ?? throw new ArgumentNullException(nameof(mousePositionExecutor));

    public async Task ExecuteAsync(RunScriptRuntimeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var steps = request.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => step.Trim())
            .ToList();

        _ = await ExecuteRangeAsync(
            steps,
            0,
            steps.Count,
            request,
            new LoopExecutionState(),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LoopControlSignal> ExecuteRangeAsync(
        IReadOnlyList<string> steps,
        int start,
        int end,
        RunScriptRuntimeExecutionRequest request,
        LoopExecutionState loopState,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];

            if (RunScriptSyntax.IsBlockEndToken(step) || RunScriptSyntax.IsElseHeader(step))
            {
                return LoopControlSignal.None;
            }

            if (TryParseBlockHeader(step, "if", out var ifCondition))
            {
                var trueStart = index + 1;
                var trueEnd = FindBlockEnd(steps, trueStart, end);
                var afterIf = trueEnd + 1;
                var falseStart = -1;
                var falseEnd = -1;
                if (afterIf < end && RunScriptSyntax.IsElseHeader(steps[afterIf]))
                {
                    falseStart = afterIf + 1;
                    falseEnd = FindBlockEnd(steps, falseStart, end);
                    afterIf = falseEnd + 1;
                }

                LoopControlSignal signal;
                if (EvaluateCondition(ifCondition))
                {
                    signal = await ExecuteRangeAsync(steps, trueStart, trueEnd, request, loopState, cancellationToken).ConfigureAwait(false);
                }
                else if (falseStart >= 0)
                {
                    signal = await ExecuteRangeAsync(steps, falseStart, falseEnd, request, loopState, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    signal = LoopControlSignal.None;
                }
                if (signal is not LoopControlSignal.None)
                {
                    return signal;
                }

                index = afterIf;
                continue;
            }

            if (TryParseBlockHeader(step, "while", out var whileCondition))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                while (EvaluateCondition(whileCondition))
                {
                    loopState.Advance();

                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, loopState, cancellationToken).ConfigureAwait(false);
                    if (signal is LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal is LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (TryParseRepeatHeader(step, out var repeatCount))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                for (var i = 0; i < repeatCount; i++)
                {
                    loopState.Advance();
                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, loopState, cancellationToken).ConfigureAwait(false);
                    if (signal is LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal is LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (TryParseForHeader(step, out var forVariableName, out var forStart, out var forEnd, out var forStep))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                if (forStep is 0)
                {
                    throw new InvalidOperationException("For step cannot be 0.");
                }

                for (var i = forStart; forStep > 0 ? i <= forEnd : i >= forEnd; i += forStep)
                {
                    loopState.Advance();
                    _runtimeVariables[forVariableName] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, loopState, cancellationToken).ConfigureAwait(false);
                    if (signal is LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal is LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (string.Equals(step, RunScriptSyntax.BreakCommand, StringComparison.OrdinalIgnoreCase))
            {
                return LoopControlSignal.Break;
            }

            if (string.Equals(step, RunScriptSyntax.ContinueCommand, StringComparison.OrdinalIgnoreCase))
            {
                return LoopControlSignal.Continue;
            }

            await ExecuteCommandAsync(step, index + 1, request, cancellationToken).ConfigureAwait(false);
            index++;
        }

        return LoopControlSignal.None;
    }

    private Task ExecuteCommandAsync(
        string step,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteCommandCoreAsync(step, stepNumber, request, cancellationToken);
    }

    private async Task ExecuteCommandCoreAsync(
        string step,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (RunScriptScreenReadExecutor.IsScreenReadingStep(step))
        {
            await _screenReadExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken, request.ImageAssets).ConfigureAwait(false);
            return;
        }

        if (RunScriptWindowExecutor.IsWindowStep(step))
        {
            await _windowExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (RunScriptSyntax.IsClipboardStep(step))
        {
            if (RunScriptClipboardExecutor.TryGetCaptureShortcut(step, out var shortcut))
            {
                _clipboardExecutor.EnsureSupported(stepNumber);
                await ExecuteCopyShortcutAsync(shortcut, stepNumber, request, cancellationToken).ConfigureAwait(false);
            }

            await _clipboardExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (RunScriptSyntax.IsShellStep(step))
        {
            await _shellExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (RunScriptPlatformSyntax.IsScreenshotStep(step))
        {
            await _screenshotExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (RunScriptSyntax.IsMousePositionStep(step))
        {
            await _mousePositionExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (TryParseDelayCommand(step, out var delayMicroseconds, request))
        {
            if (delayMicroseconds > 0)
            {
                await _timingService.WaitAsync(
                    delayMicroseconds / (double)MacroTiming.MicrosecondsPerMillisecond / request.SpeedMultiplier,
                    _pauseToken,
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (TryApplyVariableCommand(step))
        {
            return;
        }

        var resolvedStep = ResolveVariables(step);
        var compiler = new RunScriptCompiler(_keyCodeMapper);
        var compileResult = compiler.Compile([new RunScriptStep(resolvedStep)]);
        if (!compileResult.Success || compileResult.Sequence is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {compileResult.ErrorMessage}");
        }

        foreach (var ev in compileResult.Sequence.Events)
        {
            await request.ExecuteEventAsync(ev, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCopyShortcutAsync(
        string shortcut,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var compiler = new RunScriptCompiler(_keyCodeMapper);
        var compileResult = compiler.Compile([new RunScriptStep($"tap {shortcut}")]);
        if (!compileResult.Success || compileResult.Sequence is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {compileResult.ErrorMessage}");
        }

        foreach (var ev in compileResult.Sequence.Events)
        {
            await request.ExecuteEventAsync(ev, cancellationToken).ConfigureAwait(false);
        }

        await _timingService.WaitAsync(
            RunScriptClipboardExecutor.CaptureSettleDelayMilliseconds,
            _pauseToken,
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryApplyVariableCommand(string step)
    {
        if (step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var payload = step[4..].Trim();
            var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
            string variableName;
            string value;
            if (equalIndex >= 0)
            {
                variableName = payload[..equalIndex].Trim();
                value = payload[(equalIndex + 1)..].Trim();
            }
            else
            {
                var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length is not 2)
                {
                    throw new InvalidOperationException("Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.");
                }

                variableName = parts[0];
                value = parts[1];
            }

            RunScriptRuntimeText.EnsureValidVariableName(variableName);
            var resolvedValue = ResolveVariables(value);

            // Fixed divergence: evaluate numeric expressions like the compile-time path (`set x 5+3` stores 8). A surviving '$' is a '$$' escape; keep the raw fallback.
            if (!resolvedValue.Contains('$', StringComparison.Ordinal)
                && ScriptNumericExpression.TryParse(resolvedValue, out var numericExpression)
                && numericExpression is not null)
            {
                if (!ScriptNumericExpression.Evaluate(numericExpression, _runtimeVariablesView, out var numericValue, out var expressionError))
                {
                    throw new InvalidOperationException(expressionError);
                }

                _runtimeVariables[variableName] = numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                _runtimeVariables[variableName] = resolvedValue;
            }

            return true;
        }

        if (step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            var sign = step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
            var payload = step[4..].Trim();
            var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 1 or > 2)
            {
                throw new InvalidOperationException("Invalid inc/dec syntax. Expected: inc <name> [amount] or dec <name> [amount].");
            }

            var variableName = parts[0];
            RunScriptRuntimeText.EnsureValidVariableName(variableName);
            if (!_runtimeVariables.TryGetValue(variableName, out var existingValue)
                || !int.TryParse(existingValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var existingInt))
            {
                throw new InvalidOperationException($"Variable '{variableName}' must exist and be an integer for inc/dec.");
            }

            var amountToken = parts.Length is 2 ? ResolveVariables(parts[1]) : "1";
            if (!int.TryParse(amountToken, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                throw new InvalidOperationException($"Invalid inc/dec amount '{amountToken}'. Expected integer.");
            }

            _runtimeVariables[variableName] = (existingInt + (sign * amount)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("div ", StringComparison.OrdinalIgnoreCase))
        {
            var isDivide = step.StartsWith("div ", StringComparison.OrdinalIgnoreCase);
            var command = isDivide ? "div" : "mul";
            var payload = step[4..].Trim();
            var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 1 or > 2)
            {
                throw new InvalidOperationException($"Invalid {command} syntax. Expected: {command} <name> [amount].");
            }

            var variableName = parts[0];
            RunScriptRuntimeText.EnsureValidVariableName(variableName);
            if (!_runtimeVariables.TryGetValue(variableName, out var existingValue)
                || !int.TryParse(existingValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var existingInt))
            {
                throw new InvalidOperationException($"variable '{variableName}' must exist and be an integer for mul/div.");
            }

            var amountToken = parts.Length is 2 ? ResolveVariables(parts[1]) : "1";
            if (!int.TryParse(amountToken, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                throw new InvalidOperationException($"Invalid mul/div amount '{amountToken}'. Expected integer.");
            }

            if (isDivide && amount is 0)
            {
                throw new InvalidOperationException("Division by zero is not allowed in mul/div.");
            }

            var updated = isDivide
                ? (long)existingInt / amount
                : (long)existingInt * amount;
            if (updated is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException("Result is out of range for mul/div.");
            }

            _runtimeVariables[variableName] = ((int)updated).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private bool TryParseDelayCommand(string step, out long delayMicroseconds, RunScriptRuntimeExecutionRequest request)
    {
        delayMicroseconds = 0;
        if (!RunScriptSyntax.StartsWithCommandToken(step, "delay"))
        {
            return false;
        }
        if (!RunScriptInputSyntax.TryParseDelay(ResolveVariables(step), out var random, out var fixedDelay, out var minimum, out var maximum, out var error))
        {
            return false;
        }
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
        delayMicroseconds = random
            ? (long)request.ResolveDelayMs(0, true, minimum, maximum) * MacroTiming.MicrosecondsPerMillisecond
            : fixedDelay;
        return true;
    }

    private static bool TryParseBlockHeader(string step, string keyword, out string condition) =>
        RunScriptHeaderParser.TryReadConditionHeader(step, keyword, out condition) && condition.Length > 0;

    private bool TryParseRepeatHeader(string step, out int count)
    {
        count = 0;
        if (!RunScriptHeaderParser.TryParseRepeatCountToken(step, out var countToken))
        {
            return false;
        }

        // Core authority resolves; committed expressions fail loudly, plain tokens keep the legacy fallback (historical messages byte-identical).
        var evaluation = ScriptNumericExpression.Evaluate(countToken, _runtimeVariablesView, "repeat count");
        if (evaluation.Status is ScriptNumericExpressionStatus.Malformed or ScriptNumericExpressionStatus.EvaluationError)
        {
            throw new InvalidOperationException(evaluation.Error);
        }

        if (evaluation.Status is ScriptNumericExpressionStatus.Evaluated)
        {
            count = evaluation.Value;
        }
        else if (!int.TryParse(ResolveVariables(countToken), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out count))
        {
            throw new InvalidOperationException("Repeat count must be an integer >= 0.");
        }

        if (count < 0)
        {
            throw new InvalidOperationException("Repeat count must be an integer >= 0.");
        }

        return true;
    }

    private bool TryParseForHeader(string step, out string variableName, out int start, out int end, out int stepValue)
    {
        variableName = string.Empty;
        start = 0;
        end = 0;
        stepValue = 0;
        if (!RunScriptHeaderParser.TryParseForHeader(step, out var header, out var error))
        {
            return false;
        }

        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        variableName = header!.VariableName;
        start = EvaluateIntegerToken(header.StartToken, "for start");
        end = EvaluateIntegerToken(header.EndToken, "for end");
        if (header.HasExplicitStep)
        {
            stepValue = EvaluateIntegerToken(header.StepToken!, "for step");
        }
        else
        {
            stepValue = start <= end ? 1 : -1;
        }

        return true;
    }

    private int EvaluateIntegerToken(string token, string description)
    {
        // Core authority first; committed expressions fail loudly, plain tokens keep the legacy pipeline and its messages.
        var evaluation = ScriptNumericExpression.Evaluate(token, _runtimeVariablesView, description);
        if (evaluation.Status is ScriptNumericExpressionStatus.Malformed or ScriptNumericExpressionStatus.EvaluationError)
        {
            throw new InvalidOperationException(evaluation.Error);
        }

        if (evaluation.Status is ScriptNumericExpressionStatus.Evaluated)
        {
            return evaluation.Value;
        }

        return ParseInteger(ResolveVariables(token), description);
    }

    private static int ParseInteger(string value, string description)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid {description} '{value}'. Expected integer.");
        }

        return parsed;
    }

    private static int FindBlockEnd(IReadOnlyList<string> steps, int start, int end)
    {
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var step = steps[i];
            if (step.EndsWith('{')
                && (step.StartsWith("if ", StringComparison.OrdinalIgnoreCase)
                    || RunScriptSyntax.IsElseHeader(step)
                    || step.StartsWith("while ", StringComparison.OrdinalIgnoreCase)
                    || step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
                    || step.StartsWith("for ", StringComparison.OrdinalIgnoreCase)))
            {
                depth++;
                continue;
            }

            if (RunScriptSyntax.IsBlockEndToken(step))
            {
                if (depth is 0)
                {
                    return i;
                }

                depth--;
            }
        }

        throw new InvalidOperationException("Missing closing brace '}'.");
    }

    private bool EvaluateCondition(string condition)
    {
        if (!RunScriptConditionParser.TryParse(condition, out var parsedCondition, out var error) || parsedCondition == null)
        {
            throw new InvalidOperationException(error ?? "Invalid condition syntax.");
        }

        var result = ScriptConditionEvaluator.Evaluate(parsedCondition.LeftToken, parsedCondition.OperatorToken, parsedCondition.RightToken, _runtimeVariablesView);
        if (result.InvalidVariableName is { } invalidVariable)
        {
            // Preserve the runtime surface's variable-name diagnostic.
            RunScriptRuntimeText.EnsureValidVariableName(invalidVariable);
        }
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
        return result.Value;
    }

    private string ResolveVariables(string input)
    {
        return RunScriptRuntimeText.ResolveVariables(input, _runtimeVariables);
    }

}
