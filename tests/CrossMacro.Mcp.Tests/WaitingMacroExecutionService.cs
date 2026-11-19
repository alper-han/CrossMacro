namespace CrossMacro.Mcp.Tests;

internal sealed class WaitingMacroExecutionService : IMacroExecutionService
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<OperationResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<OperationResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

    public async Task<OperationResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken)
    {
        Started.SetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The operation should have been cancelled.");
        }
        catch (OperationCanceledException)
        {
            Cancelled.SetResult();
            throw;
        }
    }
}
