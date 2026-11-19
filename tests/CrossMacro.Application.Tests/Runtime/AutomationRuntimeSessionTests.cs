using CrossMacro.Application.Runtime;

namespace CrossMacro.Application.Tests.Runtime;

public sealed class AutomationRuntimeSessionTests
{
    [Fact]
    public async Task StartupAndShutdown_AreOrderedAndIdempotent()
    {
        var events = new List<string>();
        var session = new AutomationRuntimeSession([Component("first", events), Component("second", events)]);
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StartAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["start first", "start second", "stop second", "stop first"], events);
    }

    [Fact]
    public async Task FailedStartup_CleansPartiallyStartedComponentAndPredecessors()
    {
        var events = new List<string>();
        var failed = Component("failed", events) with { StartAsync = _ => throw new IOException("start failed") };
        var session = new AutomationRuntimeSession([Component("first", events), failed]);
        _ = await Assert.ThrowsAsync<IOException>(() => session.StartAsync(TestContext.Current.CancellationToken));
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
        await session.StartAsync(TestContext.Current.CancellationToken);
        events.Clear();
        await session.RunSuspendedAsync(() => { events.Add("replace"); enabled = false; return Task.CompletedTask; }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["stop second", "stop first", "replace", "start first"], events);
    }

    [Fact]
    public async Task UnresolvedLifetime_PreventsProfileReplacementAndRestart()
    {
        var events = new List<string>();
        var component = Component("scheduler", events) with { IsQuiescent = () => false };
        var session = new AutomationRuntimeSession([component]);
        await session.StartAsync(TestContext.Current.CancellationToken);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunSuspendedAsync(() =>
        { events.Add("replace"); return Task.CompletedTask; }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(["start scheduler", "stop scheduler"], events);
    }

    [Fact]
    public async Task Shutdown_WaitsForProfileReplacementAndRestoration()
    {
        var events = new List<string>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new AutomationRuntimeSession([Component("first", events)]);
        await session.StartAsync(TestContext.Current.CancellationToken);
        var profileSwitch = session.RunSuspendedAsync(async () => { entered.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System); }, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
        var stop = session.StopAsync(TestContext.Current.CancellationToken);
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
        await session.StartAsync(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AggregateException>(() => session.RunSuspendedAsync(
            () => Task.FromException(new IOException("profile failed")), TestContext.Current.CancellationToken));
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
            () => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.Equal(0, starts);
    }

    [Fact]
    public async Task Shutdown_PreventsLateQueuedProfileReplacement()
    {
        var replaced = false;
        var session = new AutomationRuntimeSession([]);
        await session.StopAsync(TestContext.Current.CancellationToken);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunSuspendedAsync(
            () => { replaced = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken));
        Assert.False(replaced);
    }

    [Fact]
    public async Task CancelledStartup_RollsBackStartedStepsWithoutStartingLaterOnes()
    {
        var events = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var second = new AutomationRuntimeComponent(
            "second",
            () => false,
            async _ =>
            {
                events.Add("start second");
                await cancellation.CancelAsync();
            },
            _ =>
            {
                events.Add("stop second");
                return Task.CompletedTask;
            });
        var session = new AutomationRuntimeSession([Component("first", events), second, Component("third", events)]);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.StartAsync(cancellation.Token));

        Assert.Equal(["start first", "start second", "stop second", "stop first"], events);
    }

    [Fact]
    public async Task Stop_AggregatesFailuresAndStillStopsEveryOwnedComponent()
    {
        var stopped = new List<string>();
        var session = new AutomationRuntimeSession(
        [
            FailingStopStep("first", stopped),
            FailingStopStep("second", stopped),
        ]);
        await session.StartAsync(TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<AggregateException>(() => session.StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal(["second", "first"], stopped);
        Assert.Equal(2, error.InnerExceptions.Count);
    }

    [Fact]
    public async Task DisposeAsync_StopsOwnedComponentsOnceAndIsIdempotent()
    {
        var events = new List<string>();
        var session = new AutomationRuntimeSession([Component("first", events)]);
        await session.StartAsync(TestContext.Current.CancellationToken);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(["start first", "stop first"], events);
    }

    private static AutomationRuntimeComponent FailingStopStep(string name, List<string> stopped) =>
        new(name, () => false, _ => Task.CompletedTask, _ =>
        {
            stopped.Add(name);
            throw new IOException($"{name} stop failed");
        });

    private static AutomationRuntimeComponent Component(string name, List<string> events)
    {
        var running = false;
        return new(name, () => running,
            _ => { events.Add($"start {name}"); running = true; return Task.CompletedTask; },
            _ => { events.Add($"stop {name}"); running = false; return Task.CompletedTask; });
    }
}
