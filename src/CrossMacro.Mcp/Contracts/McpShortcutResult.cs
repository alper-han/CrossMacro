namespace CrossMacro.Mcp.Contracts;

public sealed record McpShortcutResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpShortcutTask> Tasks,
    McpShortcutTaskRun? Run,
    McpShortcutTask? Task);
