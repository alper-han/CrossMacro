namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>Queues posted callbacks until the test runs them, so continuation marshalling can be observed.</summary>
internal sealed class DeferredSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pendingCallbacks = new();

    public TaskCompletionSource PostObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int PendingCallbacks
    {
        get
        {
            lock (_pendingCallbacks)
            {
                return _pendingCallbacks.Count;
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_pendingCallbacks)
        {
            _pendingCallbacks.Enqueue((d, state));
        }

        _ = PostObserved.TrySetResult();
    }

    public void RunAll()
    {
        while (true)
        {
            (SendOrPostCallback Callback, object? State) workItem;

            lock (_pendingCallbacks)
            {
                if (_pendingCallbacks.Count is 0)
                {
                    return;
                }

                workItem = _pendingCallbacks.Dequeue();
            }

            workItem.Callback(workItem.State);
        }
    }
}
