namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopStartupLifetimeTests
{
    [Fact]
    public async Task Shutdown_CancelsAndAwaitsStartupBeforeCompleting()
    {
        await using var lifetime = new DesktopStartupLifetime();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = lifetime.StartAsync(async token =>
        {
            using var registration = token.Register(() => canceled.TrySetResult());
            await release.Task;
            token.ThrowIfCancellationRequested();
        });
        var stop = lifetime.StopAsync();
        await canceled.Task;
        Assert.False(stop.IsCompleted);
        Assert.Same(stop, lifetime.StopAsync());
        release.SetResult();
        await stop;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
    }

    [Fact]
    public async Task ShutdownBeforeStartup_RejectsWorkWithoutInvokingFactory()
    {
        await using var lifetime = new DesktopStartupLifetime();
        await lifetime.StopAsync();
        var called = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lifetime.StartAsync(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }));
        Assert.False(called);
    }
}
