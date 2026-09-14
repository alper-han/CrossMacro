namespace CrossMacro.Mcp.Contracts.Tasks;

public sealed record McpTriggerResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpTriggerTask> Tasks,
    McpTriggerTask? Task);
