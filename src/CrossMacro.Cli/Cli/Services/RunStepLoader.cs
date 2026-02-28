using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

internal static class RunStepLoader
{
    public static async Task<RunStepLoadOutcome> LoadAsync(RunExecutionRequest request, CancellationToken cancellationToken)
    {
        var steps = new List<RunStepEntry>();
        var sourceIndex = 0;
        if (!string.IsNullOrWhiteSpace(request.StepFilePath))
        {
            if (!File.Exists(request.StepFilePath))
            {
                return RunStepLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.FileError,
                    Message = "Run steps file not found.",
                    Errors = [$"File does not exist: {request.StepFilePath}"]
                });
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(request.StepFilePath, cancellationToken);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    sourceIndex++;
                    steps.Add(new RunStepEntry(trimmed, lineIndex + 1, sourceIndex));
                }
            }
            catch (Exception ex)
            {
                return RunStepLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.FileError,
                    Message = "Failed to read run steps file.",
                    Errors = [ex.Message]
                });
            }
        }

        foreach (var step in request.Steps)
        {
            sourceIndex++;
            steps.Add(new RunStepEntry(step, null, sourceIndex));
        }

        return RunStepLoadOutcome.Ok(steps);
    }
}

internal sealed class RunStepLoadOutcome
{
    private RunStepLoadOutcome()
    {
    }

    public bool Success { get; private init; }
    public IReadOnlyList<RunStepEntry>? Steps { get; private init; }
    public MacroExecutionResult? ErrorResult { get; private init; }

    public static RunStepLoadOutcome Ok(IReadOnlyList<RunStepEntry> steps)
    {
        return new RunStepLoadOutcome
        {
            Success = true,
            Steps = steps
        };
    }

    public static RunStepLoadOutcome Fail(MacroExecutionResult errorResult)
    {
        return new RunStepLoadOutcome
        {
            Success = false,
            ErrorResult = errorResult
        };
    }
}
