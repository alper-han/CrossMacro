using CrossMacro.Application.Runtime;
namespace CrossMacro.Application.Tests.Runtime;

public sealed class AutomationRuntimeSessionTests
{
    [Fact]
    public async Task StartupAndShutdown_AreOrderedAndIdempotent()
    {
        var events = new List<string>();
        var session = new AutomationRuntimeSession([Component("first", events), Component("second", events)]);
        await session.StartAsync(CancellationToken.None);
        await session.StartAsync(CancellationToken.None);
        await session.StopAsync(CancellationToken.None);
        await session.StopAsync(CancellationToken.None);
        Assert.Equal(["start first", "start second", "stop second", "stop first"], events);
    }

    [Fact]
    public async Task FailedStartup_CleansPartiallyStartedComponentAndPredecessors()
    {
        var events = new List<string>();
        var failed = Component("failed", events) with { StartAsync = _ => throw new IOException("start failed") };
        var session = new AutomationRuntimeSession([Component("first", events), failed]);
        _ = await Assert.ThrowsAsync<IOException>(() => session.StartAsync(CancellationToken.None));
        Assert.Equal(["start first", "stop failed", "stop first"], events);
    }

    [Fact]
    public async Task ProfileSuspension_RestartsOnlyPreviouslyRunningAndEnabledComponents()
    {
        var events = new List<string>();
        var enabled = true;
        var first = Component("first", events);
        var second = Component("second", events) with { IsEnabled = () => enabled };
        var session = new AutomationRuntimeSession([first, second]);
        await session.StartAsync(CancellationToken.None);
        events.Clear();
        await session.RunSuspendedAsync(() => { events.Add("replace"); enabled = false; return Task.CompletedTask; }, cancellationToken: CancellationToken.None);
        Assert.Equal(["stop second", "stop first", "replace", "start first"], events);
    }

    [Fact]
    public async Task UnresolvedLifetime_PreventsProfileReplacementAndRestart()
    {
        var events = new List<string>();
        var component = Component("scheduler", events) with { IsQuiescent = () => false };
        var session = new AutomationRuntimeSession([component]);
        await session.StartAsync(CancellationToken.None);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunSuspendedAsync(() =>
        { events.Add("replace"); return Task.CompletedTask; }, cancellationToken: CancellationToken.None));
        Assert.Equal(["start scheduler", "stop scheduler"], events);
    }

    [Fact]
    public async Task Shutdown_WaitsForProfileReplacementAndRestoration()
    {
        var events = new List<string>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new AutomationRuntimeSession([Component("first", events)]);
        await session.StartAsync(CancellationToken.None);
        var profileSwitch = session.RunSuspendedAsync(async () => { entered.SetResult(); await release.Task; }, cancellationToken: CancellationToken.None);
        await entered.Task;
        var stop = session.StopAsync(CancellationToken.None);
        Assert.False(stop.IsCompleted);
        release.SetResult();
        await profileSwitch;
        await stop;
        Assert.Equal(["start first", "stop first", "start first", "stop first"], events);
    }

    [Fact]
    public async Task FailedProfileReplacementAndRestoration_PreserveBothErrors()
    {
        var events = new List<string>();
        var original = Component("first", events);
        var starts = 0;
        var component = original with
        {
            StartAsync = token => ++starts is 1 ? original.StartAsync(token) : Task.FromException(new InvalidOperationException("restore failed")),
        };
        var session = new AutomationRuntimeSession([component]);
        await session.StartAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<AggregateException>(() => session.RunSuspendedAsync(
            () => Task.FromException(new IOException("profile failed")), CancellationToken.None));
        Assert.Collection(error.InnerExceptions,
            first => Assert.IsType<IOException>(first), second => Assert.IsType<InvalidOperationException>(second));
    }

    [Fact]
    public async Task FailedStop_WithoutQuiescenceProbe_DoesNotRestartUncertainLifetime()
    {
        var starts = 0;
        var component = new AutomationRuntimeComponent("external", () => true,
            _ => { starts++; return Task.CompletedTask; }, _ => Task.FromException(new IOException("stop failed")));
        var session = new AutomationRuntimeSession([component]);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunSuspendedAsync(
            () => Task.CompletedTask, CancellationToken.None));
        Assert.Equal(0, starts);
    }

    [Fact]
    public async Task Shutdown_PreventsLateQueuedProfileReplacement()
    {
        var replaced = false;
        var session = new AutomationRuntimeSession([]);
        await session.StopAsync(CancellationToken.None);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunSuspendedAsync(
            () => { replaced = true; return Task.CompletedTask; }, CancellationToken.None));
        Assert.False(replaced);
    }

    private static AutomationRuntimeComponent Component(string name, List<string> events)
    {
        var running = false;
        return new(name, () => running,
            _ => { events.Add($"start {name}"); running = true; return Task.CompletedTask; },
            _ => { events.Add($"stop {name}"); running = false; return Task.CompletedTask; });
    }
}
