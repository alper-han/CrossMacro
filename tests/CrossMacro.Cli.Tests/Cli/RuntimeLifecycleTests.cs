using CrossMacro.Application.Runtime;

namespace CrossMacro.Cli.Tests;

public sealed class RuntimeLifecycleTests
{
    [Fact]
    public async Task StartAndStopAreIdempotentAndStopInReverseOrder()
    {
        var events = new List<string>();
        var lifecycle = new RuntimeLifecycle(
        [
            Step("first", events),
            Step("second", events),
        ]);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        Assert.Equal(["start:first", "start:second", "stop:second", "stop:first"], events);
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
                }),
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.StartAsync(CancellationToken.None));

        Assert.Equal(["start:first", "stop:first"], events);
    }

    [Fact]
    public async Task CancelledStartRollsBackCompletedStepsWithoutStartingLaterSteps()
    {
        var events = new List<string>();
        using var cts = new CancellationTokenSource();
        var lifecycle = new RuntimeLifecycle(
        [
            Step("first", events),
            new RuntimeLifecycleStep(
                "second",
                _ =>
                {
                    events.Add("start:second");
                    cts.Cancel();
                    return Task.CompletedTask;
                },
                _ =>
                {
                    events.Add("stop:second");
                    return Task.CompletedTask;
                }),
            Step("third", events),
        ]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lifecycle.StartAsync(cts.Token));

        Assert.Equal(["start:first", "start:second", "stop:second", "stop:first"], events);
    }

    [Fact]
    public async Task StopAggregatesCleanupErrorsAndStillAttemptsEveryStartedStep()
    {
        var stopped = new List<string>();
        var lifecycle = new RuntimeLifecycle(
        [
            new RuntimeLifecycleStep("first", _ => Task.CompletedTask, _ =>
            {
                stopped.Add("first");
                throw new InvalidOperationException("first stop failed");
            }),
            new RuntimeLifecycleStep("second", _ => Task.CompletedTask, _ =>
            {
                stopped.Add("second");
                throw new InvalidOperationException("second stop failed");
            }),
        ]);

        await lifecycle.StartAsync(CancellationToken.None);
        var error = await Assert.ThrowsAsync<AggregateException>(() => lifecycle.StopAsync(CancellationToken.None));

        Assert.Equal(["second", "first"], stopped);
        Assert.Equal(2, error.InnerExceptions.Count);
    }

    private static RuntimeLifecycleStep Step(string name, ICollection<string> events) =>
        new(
            name,
            _ =>
            {
                events.Add($"start:{name}");
                return Task.CompletedTask;
            },
            _ =>
            {
                events.Add($"stop:{name}");
                return Task.CompletedTask;
            });
}
