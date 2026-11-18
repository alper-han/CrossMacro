
namespace CrossMacro.Daemon.Tests.Services;

public sealed class OrderedWriteGateTests
{
    [Fact]
    public async Task EnterAsync_WhenCanceledWhileQueued_ShouldNotBlockFollowingWriter()
    {
        var gate = new OrderedWriteGate();
        using var first = await gate.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var canceled = EnterAsync(gate, cancellation.Token);
        var following = gate.EnterAsync(CancellationToken.None).AsTask();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        first.Dispose();

        using var followingHandle = await following.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
    }

    [Fact]
    public async Task EnterAsync_WhenCanceledAfterGrantBeforeAcquire_ShouldAdvanceGate()
    {
        var observer = new RecordingWriteGateObserver();
        var gate = new OrderedWriteGate(observer);
        using var first = await gate.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelOnce = 0;
        observer.OnWriterGranted = () =>
        {
            if (Interlocked.Exchange(ref cancelOnce, 1) is 0)
            {
                cancellation.Cancel();
            }
        };

        var canceled = EnterAsync(gate, cancellation.Token);
        var following = gate.EnterAsync(CancellationToken.None).AsTask();
        first.Dispose();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        using var followingHandle = await following.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
    }

    [Fact]
    public async Task Enter_WhenWritersQueue_ShouldReleaseThemInArrivalOrder()
    {
        var observer = new RecordingWriteGateObserver();
        var gate = new OrderedWriteGate(observer);
        var enteredOrder = new ConcurrentQueue<string>();
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = await gate.EnterAsync(CancellationToken.None);
        var firstReleased = false;

        try
        {
            var secondTask = Task.Run(async () =>
            {
                using var gateHandle = await gate.EnterAsync(CancellationToken.None);
                enteredOrder.Enqueue("second");
                _ = secondEntered.TrySetResult();
                await releaseSecond.Task;
            }, CancellationToken.None);

            await observer.WhenQueuedAsync(2).WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None);

            var thirdTask = Task.Run(async () =>
            {
                using var gateHandle = await gate.EnterAsync(CancellationToken.None);
                enteredOrder.Enqueue("third");
                _ = thirdEntered.TrySetResult();
            }, CancellationToken.None);

            await observer.WhenQueuedAsync(3).WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None);

            first.Dispose();
            firstReleased = true;

            await secondEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None);
            Assert.False(thirdEntered.Task.IsCompleted);

            _ = releaseSecond.TrySetResult();

            await thirdEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None);
            await Task.WhenAll(secondTask, thirdTask);
        }
        finally
        {
            if (!firstReleased)
            {
                first.Dispose();
            }
        }

        Assert.Equal(["second", "third"], enteredOrder.ToArray());
    }

    private static async Task<OrderedWriteGate.Releaser> EnterAsync(
        OrderedWriteGate gate,
        CancellationToken cancellationToken)
    {
        return await gate.EnterAsync(cancellationToken).ConfigureAwait(false);
    }
}
