using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

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
            Steps = steps,
        };
    }

    public static RunStepLoadOutcome Fail(MacroExecutionResult errorResult)
    {
        return new RunStepLoadOutcome
        {
            Success = false,
            ErrorResult = errorResult,
        };
    }
}
