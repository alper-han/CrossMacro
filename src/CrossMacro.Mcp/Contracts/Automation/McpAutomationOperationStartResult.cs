namespace CrossMacro.Mcp.Contracts.Automation;

/// <summary>
/// The result of attempting to reserve the single automation operation slot.
/// </summary>
public sealed record McpAutomationOperationStartResult(
    McpAutomationOperation? Operation,
    McpToolOutcome? Error)
{
    public bool Started => Operation is not null;
}
