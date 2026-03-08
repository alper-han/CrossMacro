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

            await WaitForConditionAsync(() => gate.IssuedTicketCount >= 2);

            var thirdTask = Task.Run(() =>
            {
                using var gateHandle = gate.Enter();
                enteredOrder.Enqueue("third");
                thirdEntered.TrySetResult();
            });

            await WaitForConditionAsync(() => gate.IssuedTicketCount >= 3);

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

    private static async Task WaitForConditionAsync(Func<bool> condition, int maxAttempts = 50, int delayMs = 20)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Condition was not met in expected time.");
    }
}
