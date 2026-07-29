namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunSequenceExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesInjectedInclusiveRangeAndSelectedMaximum()
    {
        var player = Substitute.For<IMacroPlayer>();
        var delays = new List<TimeSpan>();
        var requestedRange = (min: 0, max: 0);
        var executor = new RunSequenceExecutor(
            () => player,
            (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            },
            (min, max) =>
            {
                requestedRange = (min, max);
                return max;
            });

        var result = await executor.ExecuteAsync(
            new MacroSequence { Events = { new MacroEvent() } },
            speedMultiplier: 1,
            countdownSeconds: 0,
            initialDelayMs: 0,
            initialHasRandomDelay: true,
            initialRandomDelayMinMs: 2,
            initialRandomDelayMaxMs: int.MaxValue,
            CancellationToken.None);

        _ = result.Success.Should().BeTrue();
        _ = requestedRange.Should().Be((2, int.MaxValue));
        _ = delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromMilliseconds(int.MaxValue));
    }

    [Fact]
    public async Task ExecuteAsync_EqualIntMaxRandomBoundsAvoidDelegateAndOverflow()
    {
        var player = Substitute.For<IMacroPlayer>();
        var delays = new List<TimeSpan>();
        var invocationCount = 0;
        var executor = new RunSequenceExecutor(
            () => player,
            (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                invocationCount++;
                return 0;
            });

        var result = await executor.ExecuteAsync(
            new MacroSequence { Events = { new MacroEvent() } },
            speedMultiplier: 1,
            countdownSeconds: 0,
            initialDelayMs: 0,
            initialHasRandomDelay: true,
            initialRandomDelayMinMs: int.MaxValue,
            initialRandomDelayMaxMs: int.MaxValue,
            CancellationToken.None);

        _ = result.Success.Should().BeTrue();
        _ = invocationCount.Should().Be(0);
        _ = delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromMilliseconds(int.MaxValue));
    }
}
