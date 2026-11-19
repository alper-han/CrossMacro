
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Plays a runtime-backed script: reads the steps into the same node tree static expansion uses, lets the
/// shared interpreter walk it against the live playback variables, and executes each command it hands out
/// through the service executors or as compiled input events.
/// </summary>
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
    // Steps arrive with their variables already substituted, so they go straight to event compilation:
    // expanding them again would read a literal "$" produced by a "$$" escape as a new reference.
    private readonly RunScriptEventCompiler _eventCompiler = new(keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper)));
    private readonly Dictionary<string, MacroEvent[]> _compiledSteps = new(StringComparer.Ordinal);
    private readonly IPlaybackTimingService _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
    private readonly IPlaybackPauseToken _pauseToken = pauseToken ?? throw new ArgumentNullException(nameof(pauseToken));
    private readonly IDictionary<string, string> _runtimeVariables = runtimeVariables ?? throw new ArgumentNullException(nameof(runtimeVariables));
    private readonly RunScriptVariables _variables = new(runtimeVariables);
    private readonly RunScriptScreenReadExecutor _screenReadExecutor = screenReadExecutor ?? throw new ArgumentNullException(nameof(screenReadExecutor));
    private readonly RunScriptWindowExecutor _windowExecutor = windowExecutor ?? throw new ArgumentNullException(nameof(windowExecutor));
    private readonly RunScriptClipboardExecutor _clipboardExecutor = clipboardExecutor ?? throw new ArgumentNullException(nameof(clipboardExecutor));
    private readonly RunScriptShellExecutor _shellExecutor = shellExecutor ?? throw new ArgumentNullException(nameof(shellExecutor));
    private readonly RunScriptScreenshotExecutor _screenshotExecutor = screenshotExecutor ?? throw new ArgumentNullException(nameof(screenshotExecutor));
    private readonly RunScriptMousePositionExecutor _mousePositionExecutor = mousePositionExecutor ?? throw new ArgumentNullException(nameof(mousePositionExecutor));

    public async Task ExecuteAsync(RunScriptRuntimeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A step is reported by its position among the non-blank steps, block lines included.
        var steps = request.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select((step, index) => new RunScriptStep(step.Trim(), SourceIndex: index + 1))
            .ToList();
        var parsed = RunScriptTreeReader.ParseScriptNodes(steps);
        if (!parsed.Success)
        {
            throw new InvalidOperationException(parsed.ErrorMessage);
        }

        var interpreter = new RunScriptInterpreter(parsed.Nodes!, _variables, RunScriptDialect.Execution, cancellationToken);
        while (true)
        {
            switch (interpreter.MoveNext(out var command))
            {
                case RunScriptInterpreter.Status.Command:
                    await ExecuteCommandAsync(command!.Source.Step, command.Source.SourceIndex, request, cancellationToken).ConfigureAwait(false);
                    break;
                case RunScriptInterpreter.Status.Completed:
                    return;
                default:
                    throw new InvalidOperationException(interpreter.FailureMessage);
            }
        }
    }

    private async Task ExecuteCommandAsync(
        string step,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (RunScriptSyntax.IsScreenReadingStep(step))
        {
            await _screenReadExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken, request.ImageAssets).ConfigureAwait(false);
            return;
        }

        if (RunScriptSyntax.IsWindowStep(step))
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

        if (RunScriptSyntax.IsScreenshotStep(step))
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

        var cacheable = !step.Contains('$', StringComparison.Ordinal);
        var resolvedStep = cacheable ? step : RunScriptRuntimeText.ResolveVariables(step, _runtimeVariables);
        foreach (var ev in CompileStep(resolvedStep, stepNumber, cacheable))
        {
            await request.ExecuteEventAsync(ev, cancellationToken).ConfigureAwait(false);
        }
    }

    private MacroEvent[] CompileStep(string step, int stepNumber, bool cacheable)
    {
        if (cacheable && _compiledSteps.TryGetValue(step, out var cached))
        {
            return cached;
        }

        var compileResult = _eventCompiler.Compile([new RunScriptStep(step)]);
        if (!compileResult.Success || compileResult.Sequence is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {compileResult.ErrorMessage}");
        }

        MacroEvent[] events = [.. compileResult.Sequence.Events];
        if (cacheable)
        {
            _compiledSteps[step] = events;
        }

        return events;
    }

    private async Task ExecuteCopyShortcutAsync(
        string shortcut,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var ev in CompileStep($"tap {shortcut}", stepNumber, cacheable: true))
        {
            await request.ExecuteEventAsync(ev, cancellationToken).ConfigureAwait(false);
        }

        await _timingService.WaitAsync(
            RunScriptClipboardExecutor.CaptureSettleDelayMilliseconds,
            _pauseToken,
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryParseDelayCommand(string step, out long delayMicroseconds, RunScriptRuntimeExecutionRequest request)
    {
        delayMicroseconds = 0;
        if (!RunScriptSyntax.StartsWithCommandToken(step, "delay"))
        {
            return false;
        }

        if (!RunScriptInputSyntax.TryParseDelay(RunScriptRuntimeText.ResolveVariables(step, _runtimeVariables), out var random, out var fixedDelay, out var minimum, out var maximum, out var error))
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
}
