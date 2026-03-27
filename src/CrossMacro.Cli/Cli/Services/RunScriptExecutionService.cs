using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class RunScriptExecutionService : IRunScriptExecutionService
{
    private readonly RunStepCompiler _runStepCompiler;
    private readonly RunSequenceExecutor _runSequenceExecutor;
    private static readonly PlaybackValidator Validator = new(new NullMousePositionProvider("CLI Run Validation Provider"));

    public RunScriptExecutionService(
        Func<IMacroPlayer> macroPlayerFactory,
        IKeyCodeMapper keyCodeMapper,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _runStepCompiler = new RunStepCompiler(keyCodeMapper);
        _runSequenceExecutor = new RunSequenceExecutor(macroPlayerFactory, delayAsync);
    }

    public async Task<MacroExecutionResult> ExecuteAsync(RunExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var loadResult = await RunStepLoader.LoadAsync(request, cancellationToken);
        if (!loadResult.Success)
        {
            return loadResult.ErrorResult!;
        }

        var steps = loadResult.Steps!;
        if (steps.Count == 0)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "No run steps provided.",
                Errors = ["Use --step at least once."]
            };
        }

        var compileResult = _runStepCompiler.Compile(steps);
        if (!compileResult.Success)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Run script parsing failed.",
                Errors = [compileResult.ErrorMessage]
            };
        }

        var sequence = compileResult.Sequence!;
        var validation = Validator.Validate(sequence);
        if (!validation.IsValid)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Run script validation failed.",
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                Data = BuildData(
                    sequence,
                    steps.Count,
                    compileResult.InitialDelayMs,
                    compileResult.InitialHasRandomDelay,
                    compileResult.InitialRandomDelayMinMs,
                    compileResult.InitialRandomDelayMaxMs)
            };
        }

        if (request.DryRun)
        {
            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script parsed successfully (dry-run).",
                Warnings = validation.Warnings,
                Data = BuildData(
                    sequence,
                    steps.Count,
                    compileResult.InitialDelayMs,
                    compileResult.InitialHasRandomDelay,
                    compileResult.InitialRandomDelayMinMs,
                    compileResult.InitialRandomDelayMaxMs)
            };
        }

        var executionResult = await _runSequenceExecutor.ExecuteAsync(
            sequence,
            request.SpeedMultiplier,
            request.CountdownSeconds,
            compileResult.InitialDelayMs,
            compileResult.InitialHasRandomDelay,
            compileResult.InitialRandomDelayMinMs,
            compileResult.InitialRandomDelayMaxMs,
            cancellationToken);

        if (executionResult.Success)
        {
            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script execution complete.",
                Warnings = validation.Warnings,
                Data = BuildData(
                    sequence,
                    steps.Count,
                    compileResult.InitialDelayMs,
                    compileResult.InitialHasRandomDelay,
                    compileResult.InitialRandomDelayMinMs,
                    compileResult.InitialRandomDelayMaxMs)
            };
        }

        if (executionResult.IsCancelled)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Run script execution cancelled."
            };
        }

        return new MacroExecutionResult
        {
            Success = false,
            ExitCode = CliExitCode.RuntimeError,
            Message = "Run script execution failed.",
            Errors = [executionResult.ErrorMessage ?? "Unknown runtime error."],
            Warnings = validation.Warnings,
            Data = BuildData(
                sequence,
                steps.Count,
                compileResult.InitialDelayMs,
                compileResult.InitialHasRandomDelay,
                compileResult.InitialRandomDelayMinMs,
                compileResult.InitialRandomDelayMaxMs)
        };
    }

    private static object BuildData(
        MacroSequence sequence,
        int stepCount,
        int initialDelayMs,
        bool initialHasRandomDelay,
        int initialRandomDelayMinMs,
        int initialRandomDelayMaxMs)
    {
        return new
        {
            stepCount,
            eventCount = sequence.EventCount,
            totalDurationMs = sequence.TotalDurationMs,
            initialDelayMs,
            initialHasRandomDelay,
            initialRandomDelayMinMs,
            initialRandomDelayMaxMs,
            trailingDelayMs = sequence.TrailingDelayMs,
            coordinateMode = sequence.IsAbsoluteCoordinates ? "absolute" : "relative"
        };
    }
}
