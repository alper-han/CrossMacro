namespace CrossMacro.Platform.Windows.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class StaMessageThreadTests
{
    [WindowsFact]
    public async Task InvokeAsync_WhenQueuedOperationIsCancelled_DoesNotRunTheAction()
    {
        using var thread = new StaMessageThread("CrossMacro_TestClipboardSta");
        var firstActionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstAction = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstAction = thread.InvokeAsync(() =>
        {
            firstActionStarted.SetResult(true);
            if (!releaseFirstAction.Task.GetAwaiter().GetResult())
            {
                throw new InvalidOperationException("The STA test action was not released.");
            }
        }, CancellationToken.None);

        var started = await firstActionStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
        Assert.True(started);

        var secondActionCalls = 0;
        using var cancellation = new CancellationTokenSource();
        var secondAction = thread.InvokeAsync(
            () => Interlocked.Increment(ref secondActionCalls),
            cancellation.Token);

        await cancellation.CancelAsync();
        var cancellationException = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondAction);
        Assert.NotNull(cancellationException);

        releaseFirstAction.SetResult(true);
        await firstAction;

        Assert.Equal(0, Volatile.Read(ref secondActionCalls));
    }
}
