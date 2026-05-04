using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CrossMacro.Daemon.Services;

namespace CrossMacro.Daemon.Tests.Services;

public sealed class OrderedWriteGateTests
{
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
                secondTicketIssued.TrySetResult();
            }
            else if (ticket == 2)
            {
                thirdTicketIssued.TrySetResult();
            }
        };

        var first = gate.Enter();
        var firstReleased = false;

        try
        {
            var secondTask = Task.Run(async () =>
            {
                using var gateHandle = gate.Enter();
                enteredOrder.Enqueue("second");
                secondEntered.TrySetResult();
                await releaseSecond.Task;
            });

            await secondTicketIssued.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var thirdTask = Task.Run(() =>
            {
                using var gateHandle = gate.Enter();
                enteredOrder.Enqueue("third");
                thirdEntered.TrySetResult();
            });

            await thirdTicketIssued.Task.WaitAsync(TimeSpan.FromSeconds(2));

            first.Dispose();
            firstReleased = true;

            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(thirdEntered.Task.IsCompleted);

            releaseSecond.TrySetResult();

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
