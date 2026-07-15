using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Runtime;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public sealed class RunScriptRuntimeService : IRunExecutionService
{
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly IMousePositionProvider? _mousePositionProvider;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync;

    public RunScriptRuntimeService(
        Func<IMacroPlayer> macroPlayerFactory,
        IKeyCodeMapper keyCodeMapper,
        IMousePositionProvider? mousePositionProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _macroPlayerFactory = macroPlayerFactory ?? throw new ArgumentNullException(nameof(macroPlayerFactory));
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _mousePositionProvider = mousePositionProvider;
        _delayAsync = delayAsync;
    }

    public async Task<RunExecutionResult> ExecuteAsync(
        RunExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var compiler = new RunScriptCompiler(_keyCodeMapper);
        var compileResult = compiler.Compile(
            request.Steps
                .Select(step => new RunScriptStep(step.Step, step.SourceLineNumber, step.SourceIndex))
                .ToList());
        if (!compileResult.Success)
        {
            return new RunExecutionResult
            {
                Status = RunExecutionStatus.InvalidArguments,
                Errors = [compileResult.ErrorMessage],
            };
        }

        var sequence = compileResult.Sequence!;
        var validation = new PlaybackValidator(_keyCodeMapper, _mousePositionProvider).Validate(sequence);
        if (!validation.IsValid)
        {
            return CreateResult(
                RunExecutionStatus.ValidationFailed,
                sequence,
                request.Steps.Count,
                compileResult,
                validation.Errors,
                validation.Warnings);
        }

        if (request.DryRun)
        {
            return CreateResult(
                RunExecutionStatus.Succeeded,
                sequence,
                request.Steps.Count,
                compileResult,
                warnings: validation.Warnings);
        }

        var executionResult = await new RunSequenceExecutor(_macroPlayerFactory, _delayAsync).ExecuteAsync(
            sequence,
            request.SpeedMultiplier,
            request.CountdownSeconds,
            compileResult.InitialDelayMs,
            compileResult.InitialHasRandomDelay,
            compileResult.InitialRandomDelayMinMs,
            compileResult.InitialRandomDelayMaxMs,
            cancellationToken);

        var status = executionResult.Success
            ? RunExecutionStatus.Succeeded
            : executionResult.IsCancelled
                ? RunExecutionStatus.Cancelled
                : executionResult.IsAbsolutePlaybackUnsupported
                    ? RunExecutionStatus.AbsolutePlaybackUnsupported
                    : executionResult.IsInputInjectionPermissionRequired
                        ? RunExecutionStatus.InputInjectionPermissionRequired
                        : RunExecutionStatus.Failed;

        return CreateResult(
            status,
            sequence,
            request.Steps.Count,
            compileResult,
            warnings: validation.Warnings,
            runtimeVariables: executionResult.RuntimeVariables,
            errorMessage: executionResult.ErrorMessage);
    }

    private static RunExecutionResult CreateResult(
        RunExecutionStatus status,
        Core.Models.MacroSequence sequence,
        int stepCount,
        RunScriptCompileResult compileResult,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyDictionary<string, string>? runtimeVariables = null,
        string? errorMessage = null)
    {
        return new RunExecutionResult
        {
            Status = status,
            Sequence = sequence,
            StepCount = stepCount,
            InitialDelayMs = compileResult.InitialDelayMs,
            InitialHasRandomDelay = compileResult.InitialHasRandomDelay,
            InitialRandomDelayMinMs = compileResult.InitialRandomDelayMinMs,
            InitialRandomDelayMaxMs = compileResult.InitialRandomDelayMaxMs,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            RuntimeVariables = runtimeVariables ?? new Dictionary<string, string>(),
            ErrorMessage = errorMessage,
        };
    }
}
