namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A redacted automation operation lookup result.
/// </summary>
public sealed record McpAutomationGetResult(
    McpToolOutcome Outcome,
    McpAutomationOperation? Operation);
