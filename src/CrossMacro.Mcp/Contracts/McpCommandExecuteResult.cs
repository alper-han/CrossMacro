namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Redacted result from the restricted CLI compatibility adapter.
/// </summary>
public sealed record McpCommandExecuteResult(
    McpToolOutcome Outcome,
    string Command,
    bool OperationStarted,
    string? OperationId);
