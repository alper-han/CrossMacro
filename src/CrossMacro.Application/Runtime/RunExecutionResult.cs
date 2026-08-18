
namespace CrossMacro.Application.Runtime;

public sealed class RunExecutionResult
{
    public RunExecutionStatus Status { get; init; }
    public bool Success => Status is RunExecutionStatus.Succeeded;
    public MacroSequence? Sequence { get; init; }
    public int StepCount { get; init; }
    public long InitialDelayMicroseconds { get; init; }
    public int InitialDelayMs { get; init; }
    public bool InitialHasRandomDelay { get; init; }
    public int InitialRandomDelayMinMs { get; init; }
    public int InitialRandomDelayMaxMs { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyDictionary<string, string> RuntimeVariables { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? ErrorMessage { get; init; }
}
