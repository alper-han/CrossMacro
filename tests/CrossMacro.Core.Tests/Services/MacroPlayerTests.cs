namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
using FluentAssertions;
using NSubstitute;

/// <summary>
/// Tests for MacroPlayer focusing on edge cases and error handling
/// </summary>
public class MacroPlayerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);
    private readonly IMousePositionProvider _positionProvider;
    private readonly PlaybackValidator _validator;

    public MacroPlayerTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _positionProvider.IsSupported.Returns(true);
        _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _validator = new PlaybackValidator(_positionProvider);
    }

    private MacroPlayer CreatePlayer(
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null)
    {
        return new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timingService,
            playbackWaitAsync: playbackWaitAsync ?? ((_, _) => Task.CompletedTask),
            playbackElapsedMillisecondsFactory: playbackElapsedMillisecondsFactory,
            inputSimulatorFactory: inputSimulatorFactory);
    }

    [Fact]
    public async Task PlayAsync_NullMacro_ThrowsArgumentNullException()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = async () => await player.PlayAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlayAsync_EmptyMacro_ThrowsInvalidOperationException()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);
        var macro = new MacroSequence(); // Empty events

        // Act
        var act = async () => await player.PlayAsync(macro);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public void IsPlaying_Initially_IsFalse()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void IsPaused_Initially_IsFalse()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void CurrentLoop_Initially_IsZero()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.CurrentLoop.Should().Be(0);
    }

    [Fact]
    public void TotalLoops_Initially_IsZero()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Assert
        player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = () => player.Stop();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Pause_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        player.Pause();

        // Assert
        player.IsPaused.Should().BeFalse(); // Can't pause when not playing
    }

    [Fact]
    public void Resume_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        player.Resume();

        // Assert
        player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var player = new MacroPlayer(_positionProvider, _validator);

        // Act
        var act = () =>
        {
            player.Dispose();
            player.Dispose();
            player.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PlayAsync_ExecutesEvents_OnInputSimulator()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        
        var player = new MacroPlayer(
            _positionProvider, 
            _validator, 
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 100 },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left },
                new() { Type = EventType.KeyPress, KeyCode = 30 }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        // Verify MoveRelative (default mode)
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        
        // Verify MouseButton
        simulator.Received().MouseButton(Arg.Any<int>(), true);
        
        // Verify KeyPress
        simulator.Received().KeyPress(30, true);
    }

    [Fact]
    public async Task PlayAsync_WhenLooping_UsesRepeatDelayFromOptions()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 }
            }
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 123
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        timing.WaitCalls.Should().Contain(123);
    }

    [Fact]
    public async Task PlayAsync_WhenLoopingWithZeroRepeatDelay_DoesNotInjectMinimumDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 }
            }
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 0
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        timing.WaitCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenLoopingWithRandomRepeatDelay_UsesRandomDelayRange()
    {
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 }
            }
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 999,
            UseRandomRepeatDelay = true,
            RepeatDelayMinMs = 77,
            RepeatDelayMaxMs = 77
        };

        await player.PlayAsync(macro, options);

        timing.WaitCalls.Should().ContainSingle().Which.Should().Be(77);
    }

    [Fact]
    public async Task PlayAsync_WhenEventHasRandomDelay_UsesFixedPlusRandomDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 20,
                    Y = 20,
                    DelayMs = 30,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 20,
                    RandomDelayMaxMs = 20
                }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        timing.WaitCalls.Should().ContainSingle();
        timing.WaitCalls[0].Should().BeInRange(45, 50);
    }

    [Fact]
    public async Task PlayAsync_WhenFirstEventHasDelay_WaitsBeforeExecutingFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService
        {
            WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 0 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro);
        await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert (before delay released)
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());

        timing.ContinueWait.TrySetResult(true);
        await playbackTask;

        timing.WaitCalls.Should().ContainSingle();
        timing.WaitCalls[0].Should().BeInRange(39, 40);
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
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            TrailingDelayMs = 15,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 25,
            TrailingDelayMaxMs = 25,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        timing.WaitCalls.Should().ContainSingle();
        timing.WaitCalls[0].Should().BeInRange(35, 40);
    }

    [Fact]
    public async Task PlayAsync_WhenAlreadyPlaying_ThrowsInvalidOperationException()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 }
            }
        };

        // Block timing service so first playback remains in-progress.
        timing.WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        timing.ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstPlayback = player.PlayAsync(macro);
        await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        var act = async () => await player.PlayAsync(macro);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");

        player.Stop();
        timing.ContinueWait.TrySetResult(true);
        await firstPlayback;
    }

    [Fact]
    public async Task PlayAsync_WhenPausedDuringDelayAndResumed_ExecutesAllEventsInOrder()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var delayWaitEntered = new AsyncSignal();
        var releaseDelayWait = new AsyncSignal();
        var pauseObserved = new AsyncSignal();
        var timing = new ControlledTimingService();

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex == 1)
            {
                delayWaitEntered.Signal();
                await releaseDelayWait.WaitAsync(TestTimeout, cancellationToken);
                if (pauseToken.IsPaused)
                {
                    pauseObserved.Signal();
                    await pauseToken.WaitIfPausedAsync(cancellationToken);
                }
            }
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 1, Y = 1, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 3, Y = 3, DelayMs = 40 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await delayWaitEntered.WaitAsync(TestTimeout);

        player.Pause();
        player.IsPaused.Should().BeTrue();

        // Let the in-flight delay continue so pause is honored via pause token wait.
        releaseDelayWait.Signal();
        await pauseObserved.WaitAsync(TestTimeout);
        playbackTask.IsCompleted.Should().BeFalse();

        player.Resume();
        player.IsPaused.Should().BeFalse();

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

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenEventsAndResumed_ExecutesAllEventsInOrder()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var secondEventStarted = new AsyncSignal();
        var pauseObserved = new AsyncSignal();
        var timing = new ControlledTimingService();

        timing.OnWaitAsync = async (_, _, pauseToken, cancellationToken) =>
        {
            if (pauseToken.IsPaused)
            {
                pauseObserved.Signal();
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        };

        MacroPlayer? player = null;
        simulator
            .When(s => s.MoveRelative(20, 20))
            .Do(_ =>
            {
                player!.Pause();
                pauseObserved.Signal();
                secondEventStarted.Signal();
            });

        player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await pauseObserved.WaitAsync(TestTimeout);
        player.IsPaused.Should().BeTrue();

        playbackTask.IsCompleted.Should().BeFalse();

        player.Resume();
        await playbackTask;

        // Assert
        Received.InOrder(() =>
        {
            simulator.MoveRelative(10, 10);
            simulator.MoveRelative(20, 20);
            simulator.MoveRelative(30, 30);
            simulator.MoveRelative(40, 40);
        });
        simulator.Received(1).MoveRelative(10, 10);
        simulator.Received(1).MoveRelative(20, 20);
        simulator.Received(1).MoveRelative(30, 30);
        simulator.Received(1).MoveRelative(40, 40);
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
        simulator.ProviderName.Returns("MockSimulator");

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 100 }
            }
        };

        var options = new PlaybackOptions
        {
            SpeedMultiplier = speedMultiplier
        };

        // Act
        var act = async () => await player.PlayAsync(macro, options);

        // Assert
        await act.Should().NotThrowAsync();
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PlayAsync_WhenFirstDelayOverruns_ShouldCompensateBySkippingSubsequentWaits()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = (callIndex, _, _, _) =>
            {
                if (callIndex == 1)
                {
                    clock.AdvanceBy(130);
                }

                return Task.CompletedTask;
            }
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 }
            }
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        timing.WaitCalls.Should().ContainSingle();
        timing.WaitCalls[0].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlayAsync_WhenFirstEventExecutionIsSlow_ShouldStillHonorFirstScheduledDelay()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        int moveCallCount = 0;
        simulator
            .When(s => s.MoveRelative(Arg.Any<int>(), Arg.Any<int>()))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref moveCallCount) == 1)
                {
                    clock.AdvanceBy(120);
                }
            });

        var timing = new ControlledTimingService();
        timing.OnWaitAsync = (callIndex, delayMs, _, _) =>
        {
            clock.AdvanceBy(delayMs);
            return Task.CompletedTask;
        };

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 }
            }
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        timing.WaitCalls.Should().ContainInOrder(40, 40);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedAfterDelayOverrun_ResumeShouldNotBurstThroughRemainingEvents()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var secondWaitEntered = new AsyncSignal();
        var timing = new ControlledTimingService();

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex == 1)
            {
                clock.AdvanceBy(130);
                return;
            }

            if (callIndex == 2)
            {
                secondWaitEntered.Signal();
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
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
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await pausedAtSecondEvent.WaitAsync(TestTimeout);
        playbackTask.IsCompleted.Should().BeFalse();

        player.Resume();
        await secondWaitEntered.WaitAsync(TestTimeout);
        await playbackTask;

        // Assert
        timing.WaitCalls.Count.Should().BeGreaterThanOrEqualTo(2);
        timing.WaitCalls.Skip(1).Should().Contain(delay => delay > 0);
    }

    [Fact]
    public async Task PlayAsync_WhenPauseResumeCompletesInsideDelayWait_ShouldStillHonorLaterDelays()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var clock = new ManualPlaybackClock();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing,
            playbackElapsedMillisecondsFactory: clock.CreateElapsedMillisecondsProviderFactory());

        timing.OnWaitAsync = (callIndex, _, _, _) =>
        {
            if (callIndex == 1)
            {
                clock.AdvanceBy(130);
                player.Pause();
                player.Resume();
            }

            return Task.CompletedTask;
        };

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 }
            }
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        timing.WaitCalls.Count.Should().BeGreaterThanOrEqualTo(2);
        timing.WaitCalls.Skip(1).Should().Contain(delay => delay > 0);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenNonModifierKeyPressAndRelease_ShouldNotReEmitKeyPressOnResume()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex == 1)
            {
                waitEntered.Signal();
                await releaseWait.WaitAsync(TestTimeout, cancellationToken);
                if (pauseToken.IsPaused)
                {
                    paused.Signal();
                    await pauseToken.WaitIfPausedAsync(cancellationToken);
                }
            }
        };

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_A, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_A, DelayMs = 80 },
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_B, DelayMs = 80 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_B, DelayMs = 80 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await waitEntered.WaitAsync(TestTimeout);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout);
        player.Resume();
        await playbackTask;

        // Assert
        simulator.Received(1).KeyPress(InputEventCode.KEY_A, true);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenModifierKeyPressAndRelease_ShouldRestoreModifierOnResume()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex == 1)
            {
                waitEntered.Signal();
                await releaseWait.WaitAsync(TestTimeout, cancellationToken);
                if (pauseToken.IsPaused)
                {
                    paused.Signal();
                    await pauseToken.WaitIfPausedAsync(cancellationToken);
                }
            }
        };

        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_LEFTCTRL, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_LEFTCTRL, DelayMs = 100 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await waitEntered.WaitAsync(TestTimeout);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout);
        player.Resume();
        await playbackTask;

        // Assert
        simulator.Received(2).KeyPress(InputEventCode.KEY_LEFTCTRL, true);
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClick_UsesLiveCursorWithoutSyntheticMove()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.Click, Button = MouseButton.Left, UseCurrentPosition = true }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        simulator.InitializedWidth.Should().Be(0);
        simulator.InitializedHeight.Should().Be(0);
        simulator.AbsoluteMoves.Should().BeEmpty();
        simulator.ButtonTransitions.Should().HaveCount(2);
        simulator.Operations[0].Should().Be("btn:down");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickLoops_DoesNotInjectSyntheticMovement()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.Click, Button = MouseButton.Left, UseCurrentPosition = true }
            }
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 0
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        simulator.AbsoluteMoves.Should().BeEmpty();
        simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionEventHasStoredCoordinates_DoesNotMoveToStoredPosition()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 999,
                    Y = 777,
                    UseCurrentPosition = true
                }
            }
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        simulator.AbsoluteMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenMacroHasMixedCoordinateModes_ExecutesEachEventWithEffectiveMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.InitializedWidth.Should().Be(1920);
        simulator.InitializedHeight.Should().Be(1080);
        simulator.Operations.Should().ContainInOrder("abs:100,200", "rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenMacroCombinesCurrentAbsoluteAndRelativeEvents_UsesPerEventMovementSemantics()
    {
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    UseCurrentPosition = true
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MouseButton.Right,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.InitializedWidth.Should().Be(1920);
        simulator.InitializedHeight.Should().Be(1080);
        simulator.Operations.Should().Equal(
            "btn:down",
            "btn:up",
            "abs:100,200",
            "rel:10,-5",
            "btn:down",
            "btn:up");
        simulator.AbsoluteMoves.Should().Equal([(100, 200)]);
        simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenLegacyAbsoluteMacroHasExplicitRelativeEvent_UsesRelativeEventMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.InitializedWidth.Should().Be(0);
        simulator.InitializedHeight.Should().Be(0);
        simulator.AbsoluteMoves.Should().BeEmpty();
        simulator.Operations.Should().Contain("rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            }
        };

        var act = async () => await player.PlayAsync(macro);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesResolutionOnlyProvider_CachesResolutionAndCreatesAbsoluteDevice()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        resolutionOnlyProvider.IsSupported.Returns(false);
        resolutionOnlyProvider.ProviderName.Returns("Niri IPC (Resolution Only)");
        resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));

        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            resolutionOnlyProvider,
            new PlaybackValidator(resolutionOnlyProvider),
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            }
        };

        await player.PlayAsync(macro);

        await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        simulator.InitializedWidth.Should().Be(1920);
        simulator.InitializedHeight.Should().Be(1080);
        simulator.Operations.Should().Contain("abs:100,200");
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesResolutionOnlyProvider_CachesResolutionAndPlaysRelativeOnly()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        resolutionOnlyProvider.IsSupported.Returns(false);
        resolutionOnlyProvider.ProviderName.Returns("COSMIC RandR (Resolution Only)");
        resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((2560, 1440)));

        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            resolutionOnlyProvider,
            new PlaybackValidator(resolutionOnlyProvider),
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            ]
        };

        await player.PlayAsync(macro);

        await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        simulator.InitializedWidth.Should().Be(0);
        simulator.InitializedHeight.Should().Be(0);
        simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenMixedMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            }
        };

        var act = async () => await player.PlayAsync(macro);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    UseCurrentPosition = true,
                    X = 100,
                    Y = 200
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.Operations.Should().Equal("btn:down", "btn:up");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteThenRelativeMacroPlays_ExecutesExactMovementSequence()
    {
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 1000,
                    Y = 1000,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };

        await player.PlayAsync(macro);

        simulator.Operations.Should().Equal("abs:1000,1000", "rel:3,3");
    }

    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<int> WaitCalls { get; } = new();
        public TaskCompletionSource<bool>? WaitEntered { get; set; }
        public TaskCompletionSource<bool>? ContinueWait { get; set; }

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            WaitEntered?.TrySetResult(true);

            if (ContinueWait != null)
            {
                await ContinueWait.Task.WaitAsync(cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class ControlledTimingService : IPlaybackTimingService
    {
        public List<int> WaitCalls { get; } = new();
        public Func<int, int, IPlaybackPauseToken, CancellationToken, Task>? OnWaitAsync { get; set; }
        private int _waitCallCount;

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            int callIndex = ++_waitCallCount;
            if (OnWaitAsync != null)
            {
                await OnWaitAsync(callIndex, delayMs, pauseToken, cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class ManualPlaybackClock
    {
        private double _elapsedMilliseconds;

        public void AdvanceBy(double milliseconds)
        {
            _elapsedMilliseconds += milliseconds;
        }

        public Func<Func<double>> CreateElapsedMillisecondsProviderFactory()
        {
            return () => () => _elapsedMilliseconds;
        }
    }

    private sealed class TrackingInputSimulator : IInputSimulator, IInputSimulatorCapabilities
    {
        private readonly bool _forceRelativeOnly;

        public TrackingInputSimulator(bool forceRelativeOnly = false)
        {
            _forceRelativeOnly = forceRelativeOnly;
        }

        public string ProviderName => "Tracking";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates => !_forceRelativeOnly && InitializedWidth > 0 && InitializedHeight > 0;
        public int InitializedWidth { get; private set; }
        public int InitializedHeight { get; private set; }
        public List<(int X, int Y)> AbsoluteMoves { get; } = new();
        public List<(int Button, bool Pressed)> ButtonTransitions { get; } = new();
        public List<string> Operations { get; } = new();

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializedWidth = screenWidth;
            InitializedHeight = screenHeight;
        }

        public void MoveAbsolute(int x, int y)
        {
            AbsoluteMoves.Add((x, y));
            Operations.Add($"abs:{x},{y}");
        }

        public void MoveRelative(int dx, int dy)
        {
            Operations.Add($"rel:{dx},{dy}");
        }

        public void MouseButton(int button, bool pressed)
        {
            ButtonTransitions.Add((button, pressed));
            Operations.Add(pressed ? "btn:down" : "btn:up");
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }
}
