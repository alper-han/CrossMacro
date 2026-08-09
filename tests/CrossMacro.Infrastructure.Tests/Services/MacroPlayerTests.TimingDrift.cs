// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class MacroPlayerTests
{

    [Fact]
    public async Task PlayAsync_WhenLooping_UsesRepeatDelayFromOptions()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
            },
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 123,
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        _ = timing.WaitCalls.Should().Contain(123);
    }

    [Fact]
    public async Task PlayAsync_WhenLoopingWithZeroRepeatDelay_DoesNotInjectMinimumDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
            },
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 0,
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        _ = timing.WaitCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenLoopingWithRandomRepeatDelay_UsesRandomDelayRange()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
            },
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 999,
            UseRandomRepeatDelay = true,
            RepeatDelayMinMs = 77,
            RepeatDelayMaxMs = 77,
        };

        await player.PlayAsync(macro, options);

        _ = timing.WaitCalls.Should().ContainSingle().Which.Should().Be(77);
    }

    [Fact]
    public async Task PlayAsync_WhenEventHasRandomDelay_UsesFixedPlusRandomDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 20,
                    Y = 20,
                    DelayMs = 30,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 20,
                    RandomDelayMaxMs = 20,
                },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeInRange(45, 50);
    }

    [Fact]
    public async Task PlayAsync_WhenRandomDelayHasSubMillisecondFixedComponent_PreservesTheFixedPrecision()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var clock = new ManualPlaybackClock();
        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 20,
                    Y = 20,
                    DelayMicroseconds = 750,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 20,
                    RandomDelayMaxMs = 20,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeApproximately(20.75, 0.001);
    }

    [Fact]
    public async Task PlayAsync_WhenHighResolutionTimingAndSpeedProduceSubMillisecondDelay_PreservesFractionalWait()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var clock = new ManualPlaybackClock();
        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());
        var macro = new MacroSequence
        {
            SkipInitialZeroZero = true,
            Events =
            {
                new() { Type = EventType.MouseMove, X = 1, Y = 1 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 2,
                    Y = 2,
                    DelayMs = 0,
                    DelayMicroseconds = 500,
                },
            },
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 2.0 });

        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeApproximately(0.25, 0.001);
    }

    [Fact]
    public async Task PlayAsync_WhenFirstEventHasDelay_WaitsBeforeExecutingFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService
        {
            WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            SkipInitialZeroZero = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 0 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro);
        _ = await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert (before delay released)
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());

        _ = timing.ContinueWait.TrySetResult(true);
        await playbackTask;

        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeInRange(39, 40);
        Received.InOrder(() =>
        {
            simulator.MoveRelative(10, 10);
            simulator.MoveRelative(20, 20);
        });
    }

    [Fact]
    public async Task PlayAsync_WhenMacroHasTrailingRandomDelay_UsesFixedPlusRandomDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

        var macro = new MacroSequence
        {
            TrailingDelayMs = 15,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 25,
            TrailingDelayMaxMs = 25,
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeInRange(35, 40);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedDuringDelayAndResumed_ExecutesAllEventsInOrder()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var delayWaitEntered = new AsyncSignal();
        var releaseDelayWait = new AsyncSignal();
        var pauseObserved = new AsyncSignal();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
            {
                if (callIndex is 1)
                {
                    delayWaitEntered.Signal();
                    await releaseDelayWait.WaitAsync(TestTimeout, cancellationToken);
                    if (pauseToken.IsPaused)
                    {
                        pauseObserved.Signal();
                        await pauseToken.WaitIfPausedAsync(cancellationToken);
                    }
                }
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 1, Y = 1, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 3, Y = 3, DelayMs = 40 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await delayWaitEntered.WaitAsync(TestTimeout);

        player.Pause();
        _ = player.IsPaused.Should().BeTrue();

        // Let the in-flight delay continue so pause is honored via pause token wait.
        releaseDelayWait.Signal();
        await pauseObserved.WaitAsync(TestTimeout);
        _ = playbackTask.IsCompleted.Should().BeFalse();

        player.ResumePlayback();
        _ = player.IsPaused.Should().BeFalse();

        await playbackTask;

        // Assert
        Received.InOrder(() =>
        {
            simulator.MoveRelative(1, 1);
            simulator.MoveRelative(2, 2);
            simulator.MoveRelative(3, 3);
        });
        simulator.Received(1).MoveRelative(1, 1);
        simulator.Received(1).MoveRelative(2, 2);
        simulator.Received(1).MoveRelative(3, 3);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task PlayAsync_WhenSpeedMultiplierIsInvalid_NormalizesAndPlays(double speedMultiplier)
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 100 },
            },
        };

        var options = new PlaybackOptions
        {
            SpeedMultiplier = speedMultiplier,
        };

        // Act
        var act = async () => await player.PlayAsync(macro, options);

        // Assert
        _ = await act.Should().NotThrowAsync();
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PlayAsync_WhenFirstDelayOverruns_ShouldResetTimelineInsteadOfBurstingSubsequentEvents()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, _, _, _) =>
            {
                if (callIndex is 1)
                {
                    clock.AdvanceBy(130);
                }

                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
            },
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        _ = timing.WaitCalls.Should().HaveCount(2);
        _ = timing.WaitCalls.Should().OnlyContain(delay => delay > 0);
    }

    [Fact]
    public async Task PlayAsync_WhenHighSpeedPlaybackMissesAMotionDeadline_ShouldRebaseWithoutBursting()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                if (callIndex is 1)
                {
                    clock.AdvanceBy(delayMs + 12);
                }
                else
                {
                    clock.AdvanceBy(delayMs);
                }

                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 100 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 100 },
            },
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 5.0 });

        // Assert
        _ = timing.WaitCalls.Should().HaveCount(2);
        _ = timing.WaitCalls[0].Should().BeApproximately(100d / 5d, 0.000_001);
        _ = timing.WaitCalls[1].Should().BeApproximately(100d / 5d, 0.000_001);
    }

    [Fact]
    public async Task PlayAsync_WhenFirstEventExecutionIsSlow_ShouldStillHonorFirstScheduledDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        int moveCallCount = 0;
        simulator
            .When(s => s.MoveRelative(Arg.Any<int>(), Arg.Any<int>()))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref moveCallCount) is 1)
                {
                    clock.AdvanceBy(120);
                }
            });

        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                clock.AdvanceBy(delayMs);
                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
            },
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        _ = timing.WaitCalls.Should().ContainInOrder(40, 40);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedAfterDelayOverrun_ResumeShouldNotBurstThroughRemainingEvents()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var secondWaitEntered = new AsyncSignal();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
            {
                if (callIndex is 1)
                {
                    clock.AdvanceBy(130);
                    return;
                }

                if (callIndex is 2)
                {
                    secondWaitEntered.Signal();
                }

                if (pauseToken.IsPaused)
                {
                    await pauseToken.WaitIfPausedAsync(cancellationToken);
                }
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var pausedAtSecondEvent = new AsyncSignal();
        simulator
            .When(s => s.MoveRelative(20, 20))
            .Do(_ =>
            {
                player.Pause();
                pausedAtSecondEvent.Signal();
            });

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await pausedAtSecondEvent.WaitAsync(TestTimeout);
        _ = playbackTask.IsCompleted.Should().BeFalse();

        player.ResumePlayback();
        await secondWaitEntered.WaitAsync(TestTimeout);
        await playbackTask;

        // Assert
        _ = timing.WaitCalls.Count.Should().BeGreaterThanOrEqualTo(2);
        _ = timing.WaitCalls.Skip(1).Should().Contain(delay => delay > 0);
    }

    [Fact]
    public async Task PlayAsync_WhenPauseResumeCompletesInsideDelayWait_ShouldStillHonorLaterDelays()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        timing.OnWaitAsync = (callIndex, _, _, _) =>
        {
            if (callIndex is 1)
            {
                clock.AdvanceBy(130);
                player.Pause();
                player.ResumePlayback();
            }

            return Task.CompletedTask;
        };

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 },
            },
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        _ = timing.WaitCalls.Count.Should().BeGreaterThanOrEqualTo(2);
        _ = timing.WaitCalls.Skip(1).Should().Contain(delay => delay > 0);
    }

    [Fact]
    public async Task PlayAsync_WhenStallExactlyMatchesDriftThreshold_ShouldResetTimeline()
    {
        // Regression: guard used strict < so a stall exactly on the boundary was skipped.
        // 10x, 50ms source → adj=5ms, allowedDrift=Max(30,10)=30ms, stall=35ms → remaining=-30 → must reset.
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                clock.AdvanceBy(callIndex is 1 ? 35 : delayMs);
                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 1, Y = 1, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 50 },
                new() { Type = EventType.MouseMove, X = 3, Y = 3, DelayMs = 50 },
                new() { Type = EventType.MouseMove, X = 4, Y = 4, DelayMs = 50 },
            },
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 10.0 });

        _ = timing.WaitCalls.Should().OnlyContain(d => d > 0);
    }

    [Theory]
    [InlineData(0.1, 100, 2200)]
    [InlineData(0.5, 100, 410)]
    [InlineData(1.0, 100, 310)]
    [InlineData(2.0, 100, 160)]
    [InlineData(5.0, 100, 100)]
    [InlineData(10.0, 100, 50)]
    public async Task PlayAsync_WhenStallExceedsDriftThreshold_ShouldNotBurstAtAnySpeed(
        double speed, int sourceDelayMs, int stallMs)
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                clock.AdvanceBy(callIndex is 1 ? stallMs : delayMs);
                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var events = new List<MacroEvent>
        {
            new() { Type = EventType.MouseMove, X = 0, Y = 0, DelayMs = 0 },
        };
        for (int i = 1; i <= 4; i++)
        {
            events.Add(new MacroEvent { Type = EventType.MouseMove, X = i * 10, Y = i * 10, DelayMs = sourceDelayMs });
        }

        var macro = new MacroSequence();
        macro.ReplaceEvents(events);

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = speed });

        _ = timing.WaitCalls.Skip(1).Should().OnlyContain(d => d > 0,
            $"at {speed}x with {stallMs}ms stall, subsequent events must not burst");
    }

    [Theory]
    [InlineData(1.0, 100, 30)]
    [InlineData(2.0, 100, 40)]
    [InlineData(5.0, 100, 12)]
    [InlineData(10.0, 50, 15)]
    public async Task PlayAsync_WhenDriftIsBelowThreshold_ShouldPreserveCatchUpPacingWithoutFalseReset(
        double speed, int sourceDelayMs, int extraDriftMs)
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                clock.AdvanceBy(delayMs + extraDriftMs);
                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var events = new List<MacroEvent>
        {
            new() { Type = EventType.MouseMove, X = 0, Y = 0, DelayMs = 0 },
        };
        for (int i = 1; i <= 4; i++)
        {
            events.Add(new MacroEvent { Type = EventType.MouseMove, X = i * 10, Y = i * 10, DelayMs = sourceDelayMs });
        }

        var macro = new MacroSequence();
        macro.ReplaceEvents(events);
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = speed });

        _ = timing.WaitCalls.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenMultipleStallsOccurWithinOneMacroPlay_ShouldResetTimelineAfterEachExcessiveStall()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, delayMs, _, _) =>
            {
                clock.AdvanceBy(callIndex is 1 or 3 ? 310 : delayMs);
                return Task.CompletedTask;
            },
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 1, Y = 1, DelayMs = 100 },
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 100 },
                new() { Type = EventType.MouseMove, X = 3, Y = 3, DelayMs = 100 },
                new() { Type = EventType.MouseMove, X = 4, Y = 4, DelayMs = 100 },
                new() { Type = EventType.MouseMove, X = 5, Y = 5, DelayMs = 100 },
            },
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        _ = timing.WaitCalls.Should().OnlyContain(d => d > 0);
    }
}
