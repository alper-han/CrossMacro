
namespace CrossMacro.Daemon.Tests.Services;

public sealed class OrderedWriteGateTests
{
    [Fact]
    public async Task EnterAsync_WhenCanceledWhileQueued_ShouldNotBlockFollowingWriter()
    {
        var gate = new OrderedWriteGate();
        using var first = await gate.EnterAsync();
        using var cancellation = new CancellationTokenSource();

        var canceled = gate.EnterAsync(cancellation.Token).AsTask();
        var following = gate.EnterAsync().AsTask();
        cancellation.Cancel();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        first.Dispose();

        using var followingHandle = await following.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EnterAsync_WhenCanceledAfterGrantBeforeAcquire_ShouldAdvanceGate()
    {
        var gate = new OrderedWriteGate();
        using var first = await gate.EnterAsync();
        using var cancellation = new CancellationTokenSource();
        var cancelOnce = 0;
        gate.BeforeAcquire = _ =>
        {
            if (Interlocked.Exchange(ref cancelOnce, 1) is 0)
            {
                cancellation.Cancel();
            }
        };

        var canceled = gate.EnterAsync(cancellation.Token).AsTask();
        var following = gate.EnterAsync().AsTask();
        first.Dispose();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        using var followingHandle = await following.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Enter_WhenWritersQueue_ShouldReleaseThemInArrivalOrder()
    {
        var gate = new OrderedWriteGate();
        var enteredOrder = new ConcurrentQueue<string>();
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTicketIssued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdTicketIssued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                _ = secondTicketIssued.TrySetResult();
            }
            else if (ticket == 2)
            {
                _ = thirdTicketIssued.TrySetResult();
            }
        };

        var first = await gate.EnterAsync();
        var firstReleased = false;

        try
        {
            var secondTask = Task.Run(async () =>
            {
                using var gateHandle = await gate.EnterAsync();
                enteredOrder.Enqueue("second");
                _ = secondEntered.TrySetResult();
                await releaseSecond.Task;
            });

            await secondTicketIssued.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var thirdTask = Task.Run(async () =>
            {
                using var gateHandle = await gate.EnterAsync();
                enteredOrder.Enqueue("third");
                _ = thirdEntered.TrySetResult();
            });

            await thirdTicketIssued.Task.WaitAsync(TimeSpan.FromSeconds(2));

            first.Dispose();
            firstReleased = true;

            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(thirdEntered.Task.IsCompleted);

            _ = releaseSecond.TrySetResult();

            await thirdEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
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
}
