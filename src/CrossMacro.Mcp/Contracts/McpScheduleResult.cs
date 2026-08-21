namespace CrossMacro.Mcp.Contracts;

public sealed record McpScheduleResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpScheduleTask> Tasks,
    McpScheduleTaskRun? Run,
    McpScheduleTask? Task);
