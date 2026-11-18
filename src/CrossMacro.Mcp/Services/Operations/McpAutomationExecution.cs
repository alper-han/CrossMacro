namespace CrossMacro.Mcp.Services.Operations;

/// <summary>Shared operation lifetime and finite execution policy for both MCP automation entrances.</summary>
public sealed class McpAutomationExecution(
    IMacroExecutionService macros,
    IRunScriptExecutionService scripts,
    IRecordExecutionService recording,
    IMcpOperationCoordinator operations,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public McpAutomationOperationStartResult Start(McpAutomationOperationKind kind,
        Func<CancellationToken, Task<OperationResult>> execute, CancellationToken requestCancellation)
    {
        requestCancellation.ThrowIfCancellationRequested();
        return operations.Start(kind, execute, CancellationToken.None);
    }

    public Task<OperationResult> PlayAsync(MacroExecutionRequest request, int timeoutSeconds, CancellationToken cancellationToken) =>
        RunWithTimeoutAsync(timeoutSeconds, token => macros.ExecuteAsync(request, token), cancellationToken);

    public Task<OperationResult> RunAsync(RunScriptExecutionRequest request, int timeoutSeconds, CancellationToken cancellationToken) =>
        RunWithTimeoutAsync(timeoutSeconds, token => scripts.ExecuteAsync(request, token), cancellationToken);

    public Task<OperationResult> RecordAsync(RecordExecutionRequest request, CancellationToken cancellationToken) =>
        recording.ExecuteAsync(request, cancellationToken);

    private async Task<OperationResult> RunWithTimeoutAsync(int timeoutSeconds,
        Func<CancellationToken, Task<OperationResult>> execute, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeoutSeconds, 0);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds), _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var result = await execute(linked.Token).ConfigureAwait(false);
            if (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return TimedOut();
            }
            return result;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOut();
        }
    }

    private static OperationResult TimedOut() =>
        OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Automation operation timed out.");
}
