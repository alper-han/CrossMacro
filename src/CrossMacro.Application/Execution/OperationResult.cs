namespace CrossMacro.Application.Execution;

/// <summary>
/// The host-independent outcome of one application operation. Each host maps it to its own
/// response: the CLI to an exit code and text or JSON output, MCP to a tool outcome.
/// </summary>
public sealed class OperationResult
{
    public required bool Success { get; init; }

    public required ExecutionOutcomeCode Outcome { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public object? Data { get; init; }

    public static OperationResult Ok(
        string message,
        object? data = null,
        IReadOnlyList<string>? warnings = null) =>
        new()
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = message,
            Data = data,
            Warnings = warnings ?? [],
        };

    public static OperationResult Fail(
        ExecutionOutcomeCode outcome,
        string message,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        object? data = null) =>
        new()
        {
            Success = false,
            Outcome = outcome,
            Message = message,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            Data = data,
        };
}
