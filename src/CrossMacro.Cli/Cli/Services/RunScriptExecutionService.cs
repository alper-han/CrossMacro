using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Runtime;
using CrossMacro.Cli.Serialization;
using CrossMacro.Core.Models;

namespace CrossMacro.Cli.Services;

public sealed class RunScriptExecutionService : IRunScriptExecutionService
{
    private readonly IRunExecutionService _runtimeService;

    public RunScriptExecutionService(IRunExecutionService runtimeService)
    {
        _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));
    }

    public async Task<MacroExecutionResult> ExecuteAsync(
        RunExecutionRequest request,
        CancellationToken cancellationToken)
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

        var result = await _runtimeService.ExecuteAsync(new Application.Runtime.RunExecutionRequest(
            steps.Select(step => new RunScriptInputStep(step.Step, step.FileLineNumber, step.SourceIndex)).ToList(),
            request.SpeedMultiplier,
            request.CountdownSeconds,
            request.DryRun), cancellationToken);

        if (result.Status == RunExecutionStatus.InvalidArguments)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Run script parsing failed.",
                Errors = result.Errors
            };
        }

        var data = result.Sequence is null
            ? null
            : BuildData(result.Sequence, result.StepCount, result, result.RuntimeVariables);

        return result.Status switch
        {
            RunExecutionStatus.ValidationFailed => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Run script validation failed.",
                Errors = result.Errors,
                Warnings = result.Warnings,
                Data = data
            },
            RunExecutionStatus.Cancelled => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Run script execution cancelled."
            },
            RunExecutionStatus.AbsolutePlaybackUnsupported => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Absolute coordinate playback is not supported in this session.",
                Errors = ["This run script contains absolute mouse coordinates, but the active backend cannot play absolute coordinates. Use a backend/session with absolute coordinate support or change the script to use relative coordinates."],
                Warnings = result.Warnings,
                Data = data
            },
            RunExecutionStatus.InputInjectionPermissionRequired => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Playback permission is missing.",
                Errors = [result.ErrorMessage ?? "macOS playback permission is missing."],
                Warnings = result.Warnings,
                Data = data
            },
            RunExecutionStatus.Succeeded when request.DryRun => new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script parsed successfully (dry-run).",
                Warnings = result.Warnings,
                Data = data
            },
            RunExecutionStatus.Succeeded => new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script execution complete.",
                Warnings = result.Warnings,
                Data = data
            },
            _ => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Run script execution failed.",
                Errors = [result.ErrorMessage ?? "Unknown runtime error."],
                Warnings = result.Warnings,
                Data = data
            }
        };
    }

    private static RunScriptExecutionData BuildData(
        MacroSequence sequence,
        int stepCount,
        Application.Runtime.RunExecutionResult result,
        IReadOnlyDictionary<string, string>? runtimeVariables = null)
    {
        var coordinateMode = MacroPositionSemantics.GetCoordinateModeSummary(sequence) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            _ => "none"
        };

        return new RunScriptExecutionData(
            stepCount,
            sequence.EventCount,
            sequence.TotalDurationMs,
            result.InitialDelayMs,
            result.InitialHasRandomDelay,
            result.InitialRandomDelayMinMs,
            result.InitialRandomDelayMaxMs,
            sequence.TrailingDelayMs,
            coordinateMode,
            runtimeVariables ?? new Dictionary<string, string>());
    }
}
