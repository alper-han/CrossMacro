using CrossMacro.Application.Runtime;

namespace CrossMacro.UI.Tests.Services;

public sealed class RuntimeLifecycleTests
{
    [Fact]
    public async Task StartAndStopUsesOrderedStepsAndReverseCleanup()
    {
        var events = new List<string>();
        var lifecycle = new RuntimeLifecycle(
        [
            Step("first", events),
            Step("second", events),
            Step("third", events)
        ]);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        Assert.Equal(
            ["start:first", "start:second", "start:third", "stop:third", "stop:second", "stop:first"],
            events);
    }

    [Fact]
    public async Task FailedStartRollsBackCompletedSteps()
    {
        var events = new List<string>();
        var lifecycle = new RuntimeLifecycle(
        [
            Step("first", events),
            new RuntimeLifecycleStep(
                "second",
                _ => throw new InvalidOperationException("start failed"),
                _ =>
                {
                    events.Add("stop:second");
                    return Task.CompletedTask;
                })
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.StartAsync(CancellationToken.None));

        Assert.Equal(["start:first", "stop:first"], events);
    }

    [Fact]
    public async Task StopAggregatesCleanupErrorsAndAttemptsEveryStep()
    {
        var stopped = new List<string>();
        var lifecycle = new RuntimeLifecycle(
        [
            FailingStopStep("first", stopped),
            FailingStopStep("second", stopped)
        ]);

        await lifecycle.StartAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<AggregateException>(() => lifecycle.StopAsync(CancellationToken.None));

        Assert.Equal(["second", "first"], stopped);
        Assert.Equal(2, error.InnerExceptions.Count);
    }

    private static RuntimeLifecycleStep Step(string name, ICollection<string> events) =>
        new(name, _ =>
        {
            events.Add($"start:{name}");
            return Task.CompletedTask;
        }, _ =>
        {
            events.Add($"stop:{name}");
            return Task.CompletedTask;
        });

    private static RuntimeLifecycleStep FailingStopStep(string name, ICollection<string> stopped) =>
        new(name, _ => Task.CompletedTask, _ =>
        {
            stopped.Add(name);
            throw new InvalidOperationException($"{name} stop failed");
        });
}
