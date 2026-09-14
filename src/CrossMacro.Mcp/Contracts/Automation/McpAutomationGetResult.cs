namespace CrossMacro.Mcp.Contracts.Automation;

/// <summary>
/// A redacted automation operation lookup result.
/// </summary>
public sealed record McpAutomationGetResult(
    McpToolOutcome Outcome,
    McpAutomationOperation? Operation);
