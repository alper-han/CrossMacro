namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The result of a cancellation request. Repeated requests remain successful
/// for an existing operation and report that no additional cancellation began.
/// </summary>
public sealed record McpAutomationOperationStopResult(
    McpAutomationOperation? Operation,
    bool CancellationInitiated)
{
    public bool Found => Operation is not null;
}
