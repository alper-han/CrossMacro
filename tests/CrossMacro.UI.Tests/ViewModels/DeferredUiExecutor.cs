namespace CrossMacro.UI.Tests.ViewModels;

internal sealed class DeferredUiExecutor : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pendingCallbacks = new();

    public TaskCompletionSource<bool> PostObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_pendingCallbacks)
        {
            _pendingCallbacks.Enqueue((d, state));
        }

        _ = PostObserved.TrySetResult(true);
    }

    public void RunAll()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(this);
        try
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
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}
