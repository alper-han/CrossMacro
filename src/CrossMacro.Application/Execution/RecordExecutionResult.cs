
namespace CrossMacro.Application.Execution;

public sealed class RecordExecutionResult
{
    public bool Success { get; init; }
    public ExecutionOutcomeCode ExitCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public object? Data { get; init; }
}
