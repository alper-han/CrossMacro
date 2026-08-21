namespace CrossMacro.Mcp.Contracts;

public sealed record McpTriggerResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpTriggerTask> Tasks,
    McpTriggerTask? Task);
