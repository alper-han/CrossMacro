using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Tests.Helpers;

public sealed class DebouncedSaveCoordinatorTests
{
    [Fact]
    public async Task RequestAsync_CoalescesAChangeBurstIntoOneSave()
    {
        var saveCount = 0;
        using var coordinator = new DebouncedSaveCoordinator(
            () =>
            {
                _ = Interlocked.Increment(ref saveCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(25));

        var firstRequest = coordinator.RequestAsync();
        var secondRequest = coordinator.RequestAsync();

        _ = secondRequest.Should().BeSameAs(firstRequest);

        var flushed = await coordinator.FlushAsync(CancellationToken.None);
        await firstRequest;

        _ = flushed.Should().BeTrue();
        _ = Volatile.Read(ref saveCount).Should().Be(1);
    }

    [Fact]
    public async Task Dispose_WaitsForAndCompletesPendingSave()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var coordinator = new DebouncedSaveCoordinator(
            async () =>
            {
                _ = saveStarted.TrySetResult();
                await allowSave.Task.ConfigureAwait(false);
            },
            TimeSpan.FromSeconds(1));

        var request = coordinator.RequestAsync();
        var disposeTask = Task.Run(coordinator.Dispose, CancellationToken.None);

        await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
        _ = disposeTask.IsCompleted.Should().BeFalse();

        _ = allowSave.TrySetResult();
        await request;
        await disposeTask;
    }

    [Fact]
    public async Task FlushAsync_PropagatesSaveFailureToFlushAndRequest()
    {
        var failure = new IOException("disk full");
        using var coordinator = new DebouncedSaveCoordinator(
            () => Task.FromException(failure),
            TimeSpan.FromSeconds(1));

        var request = coordinator.RequestAsync();

        var flushException = await Assert.ThrowsAsync<IOException>(
            async () => await coordinator.FlushAsync(CancellationToken.None));
        _ = flushException.Should().BeSameAs(failure);

        var requestException = await Assert.ThrowsAsync<IOException>(async () => await request);
        _ = requestException.Should().BeSameAs(failure);
    }
}
