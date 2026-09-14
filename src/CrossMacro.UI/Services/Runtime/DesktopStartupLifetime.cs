namespace CrossMacro.UI.Services.Runtime;

/// <summary>Owns startup admission, cancellation, and completion before desktop services are released.</summary>
internal sealed class DesktopStartupLifetime : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private TaskCompletionSource? _completion;
    private Task? _stopTask;

    public Task StartAsync(Func<CancellationToken, Task> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_stopTask is not null)
            {
                return Task.FromCanceled(new CancellationToken(canceled: true));
            }
            if (_completion is not null)
            {
                return _completion.Task;
            }
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
        }
        _ = CompleteStartAsync(start, completion);
        return completion.Task;
    }

    private async Task CompleteStartAsync(Func<CancellationToken, Task> start, TaskCompletionSource completion)
    {
        try
        {
            await start(_cancellation.Token).ConfigureAwait(false);
            _ = completion.TrySetResult();
        }
        catch (OperationCanceledException exception) when (_cancellation.IsCancellationRequested)
        {
            _ = completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _ = completion.TrySetException(exception);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            return _stopTask ??= StopCoreAsync(_completion?.Task ?? Task.CompletedTask);
        }
    }

    private async Task StopCoreAsync(Task startup)
    {
        try
        {
            try
            {
                await _cancellation.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await startup.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    // Cancellation is the expected shutdown result.
                }
            }
        }
        finally
        {
            _cancellation.Dispose();
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
