namespace CrossMacro.Mcp.Contracts.Automation;

/// <summary>
/// The result of requesting cancellation for an automation operation.
/// </summary>
public sealed record McpAutomationStopResult(
    McpToolOutcome Outcome,
    McpAutomationOperation? Operation,
    bool CancellationInitiated);
