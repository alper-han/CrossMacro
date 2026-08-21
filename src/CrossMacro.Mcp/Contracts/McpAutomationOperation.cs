namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A redacted MCP automation operation snapshot. It intentionally excludes the
/// original arguments and CLI result data, which can contain sensitive content.
/// </summary>
public sealed record McpAutomationOperation(
    string OperationId,
    McpAutomationOperationKind Kind,
    McpAutomationOperationState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool CancellationRequested,
    McpToolOutcome? Outcome);
