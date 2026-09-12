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
            initialDelayMicroseconds: 0,
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
            initialDelayMicroseconds: 0,
            initialHasRandomDelay: true,
            initialRandomDelayMinMs: int.MaxValue,
            initialRandomDelayMaxMs: int.MaxValue,
            CancellationToken.None);

        _ = result.Success.Should().BeTrue();
        _ = invocationCount.Should().Be(0);
        _ = delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromMilliseconds(int.MaxValue));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInitialDelaySumOverflows_ReturnsFailureInsteadOfSkippingDelay()
    {
        var player = Substitute.For<IMacroPlayer>();
        var delays = new List<TimeSpan>();
        var executor = new RunSequenceExecutor(
            () => player,
            (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            },
            (_, _) => 1);

        var result = await executor.ExecuteAsync(
            new MacroSequence { Events = { new MacroEvent() } },
            speedMultiplier: 1,
            countdownSeconds: 0,
            initialDelayMicroseconds: long.MaxValue,
            initialHasRandomDelay: true,
            initialRandomDelayMinMs: 1,
            initialRandomDelayMaxMs: 1,
            CancellationToken.None);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _ = delays.Should().BeEmpty();
        _ = player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSubMillisecondInitialDelay()
    {
        var player = Substitute.For<IMacroPlayer>();
        var delays = new List<TimeSpan>();
        var executor = new RunSequenceExecutor(
            () => player,
            (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            });

        var result = await executor.ExecuteAsync(
            new MacroSequence { Events = { new MacroEvent() } },
            speedMultiplier: 1,
            countdownSeconds: 0,
            initialDelayMicroseconds: 500,
            initialHasRandomDelay: false,
            initialRandomDelayMinMs: 0,
            initialRandomDelayMaxMs: 0,
            CancellationToken.None);

        _ = result.Success.Should().BeTrue();
        _ = delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromMicroseconds(500));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlaybackIsCanceled_StopsPlayerAndReturnsCanceled()
    {
        var player = Substitute.For<IMacroPlayer>();
        var playbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var playbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = player.PlayAsync(
                Arg.Any<MacroSequence>(),
                Arg.Any<PlaybackOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                playbackStarted.TrySetResult();
                return playbackCompletion.Task;
            });
        var executor = new RunSequenceExecutor(() => player);
        using var cancellation = new CancellationTokenSource();

        var executionTask = executor.ExecuteAsync(
            new MacroSequence { Events = { new MacroEvent() } },
            speedMultiplier: 1,
            countdownSeconds: 0,
            initialDelayMicroseconds: 0,
            initialHasRandomDelay: false,
            initialRandomDelayMinMs: 0,
            initialRandomDelayMaxMs: 0,
            cancellation.Token);
        await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);

        await cancellation.CancelAsync();
        player.Received(1).StopPlayback();
        playbackCompletion.TrySetCanceled(cancellation.Token);

        var result = await executionTask;

        _ = result.Success.Should().BeFalse();
        _ = result.IsCancelled.Should().BeTrue();
    }
}
