namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A structured result from a window mutation. Selector values and backend
/// details are intentionally not echoed.
/// </summary>
public sealed record McpWindowControlResult(
    McpToolOutcome Outcome,
    string Action,
    bool? Changed,
    string? Workspace,
    McpWindowInfo? Window);
