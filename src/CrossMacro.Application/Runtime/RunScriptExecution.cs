using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Application.Runtime;

public interface IRunExecutionService
{
    Task<RunExecutionResult> ExecuteAsync(
        RunExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RunScriptInputStep(
    string Step,
    int? SourceLineNumber = null,
    int SourceIndex = 0);

public sealed record RunExecutionRequest(
    IReadOnlyList<RunScriptInputStep> Steps,
    double SpeedMultiplier = 1.0,
    int CountdownSeconds = 0,
    bool DryRun = false);

public enum RunExecutionStatus
{
    Succeeded,
    InvalidArguments,
    ValidationFailed,
    Cancelled,
    AbsolutePlaybackUnsupported,
    InputInjectionPermissionRequired,
    Failed,
}

public sealed class RunExecutionResult
{
    public RunExecutionStatus Status { get; init; }
    public bool Success => Status is RunExecutionStatus.Succeeded;
    public MacroSequence? Sequence { get; init; }
    public int StepCount { get; init; }
    public int InitialDelayMs { get; init; }
    public bool InitialHasRandomDelay { get; init; }
    public int InitialRandomDelayMinMs { get; init; }
    public int InitialRandomDelayMaxMs { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyDictionary<string, string> RuntimeVariables { get; init; } = new Dictionary<string, string>();
    public string? ErrorMessage { get; init; }
}
