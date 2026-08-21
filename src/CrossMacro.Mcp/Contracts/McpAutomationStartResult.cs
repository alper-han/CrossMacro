namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The result of creating an automation operation. The operation contains no
/// original arguments, scripts, file paths, or raw execution data.
/// </summary>
public sealed record McpAutomationStartResult(
    McpToolOutcome Outcome,
    McpAutomationOperation? Operation);
