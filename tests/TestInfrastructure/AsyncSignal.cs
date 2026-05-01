using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.TestInfrastructure;

public sealed class AsyncSignal
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool> _completionSource = CreateCompletionSource();

    public bool IsSignaled
    {
        get
        {
            lock (_sync)
            {
                return _completionSource.Task.IsCompletedSuccessfully;
            }
        }
    }

    public void Signal()
    {
        lock (_sync)
        {
            _completionSource.TrySetResult(true);
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Task waitTask;
        lock (_sync)
        {
            waitTask = _completionSource.Task;
        }

        try
        {
            await waitTask.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"Async signal was not received within {timeout}.", exception);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            if (!_completionSource.Task.IsCompleted)
            {
                return;
            }

            _completionSource = CreateCompletionSource();
        }
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
