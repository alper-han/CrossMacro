namespace CrossMacro.Mcp.Services;

/// <summary>
/// Coordinates the single active play, run, or record workload owned by one MCP
/// server process.
/// </summary>
public interface IMcpOperationCoordinator : IDisposable
{
    public McpAutomationOperationStartResult Start(
        McpAutomationOperationKind kind,
        Func<CancellationToken, Task<CliCommandExecutionResult>> executeAsync,
        CancellationToken cancellationToken = default);

    public McpAutomationOperation? GetOperation(string operationId);

    public McpAutomationOperation? GetActive();

    public McpAutomationOperationStopResult StopOperation(string operationId);
}
