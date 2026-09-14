namespace CrossMacro.UI.Tests.ViewModels;

internal sealed class DeferredUiExecutor : SynchronizationContext, IUiDispatcher
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pendingCallbacks = new();
    private TaskCompletionSource<int> _nextPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _postCount;

    public TaskCompletionSource<bool> PostObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int PostCount
    {
        get
        {
            lock (_pendingCallbacks)
            {
                return _postCount;
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        TaskCompletionSource<int> postSignal;
        int postCount;
        lock (_pendingCallbacks)
        {
            _pendingCallbacks.Enqueue((d, state));
            postCount = ++_postCount;
            postSignal = _nextPost;
            _nextPost = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        postSignal.SetResult(postCount);
        _ = PostObserved.TrySetResult(true);
    }

    public Task WaitForPostAfterAsync(int previousPostCount, CancellationToken cancellationToken)
    {
        Task<int> nextPost;
        lock (_pendingCallbacks)
        {
            if (_postCount > previousPostCount)
            {
                return Task.CompletedTask;
            }

            nextPost = _nextPost.Task;
        }

        return nextPost.WaitAsync(cancellationToken);
    }

    public bool CheckAccess() => ReferenceEquals(Current, this);

    public void Post(Action action) => Post(unusedState => action(), state: null);

    public Task InvokeAsync(Action action) => InvokeAsync(() => { action(); return Task.CompletedTask; });

    public Task InvokeAsync(Func<Task> action)
    {
        if (CheckAccess()) { return action(); }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(unusedState => { _ = CompleteAsync(); }, state: null);
        return completion.Task;

        async Task CompleteAsync()
        {
            try { await action(); completion.SetResult(); }
            catch (OperationCanceledException) { completion.SetCanceled(); }
            catch (Exception error) when (error is not OutOfMemoryException) { completion.SetException(error); }
        }
    }

    public async Task<T> InvokeAsync<T>(Func<T> callback)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await InvokeAsync(() => result.SetResult(callback()));
        return await result.Task;
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
