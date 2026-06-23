using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed class RunScriptExecutionService : IRunScriptExecutionService
{
    private readonly RunStepCompiler _runStepCompiler;
    private readonly RunSequenceExecutor _runSequenceExecutor;
    private readonly PlaybackValidator _validator;

    public RunScriptExecutionService(
        Func<IMacroPlayer> macroPlayerFactory,
        IKeyCodeMapper keyCodeMapper,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentNullException.ThrowIfNull(keyCodeMapper);

        _runStepCompiler = new RunStepCompiler(keyCodeMapper);
        _runSequenceExecutor = new RunSequenceExecutor(macroPlayerFactory, delayAsync);
        _validator = new PlaybackValidator(
            keyCodeMapper,
            new NullMousePositionProvider("CLI Run Validation Provider"));
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
        var validation = _validator.Validate(sequence);
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
                    compileResult.InitialRandomDelayMaxMs,
                    executionResult.RuntimeVariables)
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

        if (executionResult.IsAbsolutePlaybackUnsupported)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Absolute coordinate playback is not supported in this session.",
                Errors = ["This run script contains absolute mouse coordinates, but the active backend cannot play absolute coordinates. Use a backend/session with absolute coordinate support or change the script to use relative coordinates."],
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

        if (executionResult.IsInputInjectionPermissionRequired)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Playback permission is missing.",
                Errors = [executionResult.ErrorMessage ?? "macOS playback permission is missing."],
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
        int initialRandomDelayMaxMs,
        IReadOnlyDictionary<string, string>? runtimeVariables = null)
    {
        var coordinateMode = MacroPositionSemantics.GetCoordinateModeSummary(sequence) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            _ => "none"
        };

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
            coordinateMode,
            runtimeVariables = runtimeVariables ?? new Dictionary<string, string>()
        };
    }
}
