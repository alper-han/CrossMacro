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
        Func<CancellationToken, Task<CliCommandExecutionResult>> execute, CancellationToken requestCancellation)
    {
        requestCancellation.ThrowIfCancellationRequested();
        return operations.Start(kind, execute, CancellationToken.None);
    }

    public Task<CliCommandExecutionResult> PlayAsync(MacroExecutionRequest request, int timeoutSeconds, CancellationToken cancellationToken) =>
        RunWithTimeoutAsync(timeoutSeconds, token => macros.ExecuteAsync(request, token), cancellationToken);

    public Task<CliCommandExecutionResult> RunAsync(RunCliExecutionRequest request, int timeoutSeconds, CancellationToken cancellationToken) =>
        RunWithTimeoutAsync(timeoutSeconds, token => scripts.ExecuteAsync(request, token), cancellationToken);

    public async Task<CliCommandExecutionResult> RecordAsync(RecordExecutionRequest request, CancellationToken cancellationToken)
    {
        var result = await recording.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }

    private async Task<CliCommandExecutionResult> RunWithTimeoutAsync(int timeoutSeconds,
        Func<CancellationToken, Task<MacroExecutionResult>> execute, CancellationToken cancellationToken)
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
            return result.Success
                ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
                : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOut();
        }
    }

    private static CliCommandExecutionResult TimedOut() =>
        CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Automation operation timed out.");
}
