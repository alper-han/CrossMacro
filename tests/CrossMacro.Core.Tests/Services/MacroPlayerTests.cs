namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using FluentAssertions;
using NSubstitute;

/// <summary>
/// Tests for MacroPlayer focusing on edge cases and error handling
/// </summary>
public class MacroPlayerTests
{
    private readonly IMousePositionProvider _positionProvider;
    private readonly PlaybackValidator _validator;

    public MacroPlayerTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _positionProvider.IsSupported.Returns(true);
        _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _validator = new PlaybackValidator(_positionProvider);
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
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 1, Y = 1, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 3, Y = 3, DelayMs = 40 }
            }
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        player.Pause();
        player.IsPaused.Should().BeTrue();

        // Let the in-flight delay continue so pause is honored via pause token wait.
        timing.ContinueWait.TrySetResult(true);
        await Task.Delay(30);
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

        var secondEventStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseSecondEvent = new ManualResetEventSlim(false);
        int moveCallCount = 0;

        simulator
            .When(s => s.MoveRelative(Arg.Any<int>(), Arg.Any<int>()))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref moveCallCount) == 2)
                {
                    secondEventStarted.TrySetResult(true);
                    releaseSecondEvent.Wait(TimeSpan.FromSeconds(2));
                }
            });

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            inputSimulatorFactory: () => simulator);

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
        await secondEventStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        player.Pause();
        player.IsPaused.Should().BeTrue();

        releaseSecondEvent.Set();
        await Task.Delay(30);
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
        var timing = new OverrunningTimingService(firstWaitActualDelayMs: 130);
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
        int moveCallCount = 0;
        simulator
            .When(s => s.MoveRelative(Arg.Any<int>(), Arg.Any<int>()))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref moveCallCount) == 1)
                {
                    System.Threading.Thread.Sleep(120);
                }
            });

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
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 }
            }
        };

        // Act
        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });

        // Assert
        timing.WaitCalls.Should().Contain(delay => delay >= 30);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedAfterDelayOverrun_ResumeShouldNotBurstThroughRemainingEvents()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        simulator.ProviderName.Returns("MockSimulator");
        var timing = new OverrunningTimingService(firstWaitActualDelayMs: 130);
        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        var pausedAtSecondEvent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        simulator
            .When(s => s.MoveRelative(20, 20))
            .Do(_ =>
            {
                player.Pause();
                pausedAtSecondEvent.TrySetResult(true);
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
        await pausedAtSecondEvent.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        playbackTask.IsCompleted.Should().BeFalse();

        player.Resume();
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
        var timing = new HookedTimingService();

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        timing.OnFirstWaitAsync = async () =>
        {
            await Task.Delay(130);
            player.Pause();
            await Task.Delay(30);
            player.Resume();
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
        var timing = new HookedTimingService();
        var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        timing.OnFirstWaitAsync = () =>
        {
            player.Pause();
            paused.TrySetResult(true);
            return Task.CompletedTask;
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
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
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
        var timing = new HookedTimingService();
        var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var player = new MacroPlayer(
            _positionProvider,
            _validator,
            timingService: timing,
            inputSimulatorFactory: () => simulator);

        timing.OnFirstWaitAsync = () =>
        {
            player.Pause();
            paused.TrySetResult(true);
            return Task.CompletedTask;
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
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        player.Resume();
        await playbackTask;

        // Assert
        simulator.Received(2).KeyPress(InputEventCode.KEY_LEFTCTRL, true);
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

    private sealed class OverrunningTimingService : IPlaybackTimingService
    {
        private readonly int _firstWaitActualDelayMs;
        private int _waitCallCount;

        public OverrunningTimingService(int firstWaitActualDelayMs)
        {
            _firstWaitActualDelayMs = firstWaitActualDelayMs;
        }

        public List<int> WaitCalls { get; } = new();

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            _waitCallCount++;

            if (_waitCallCount == 1)
            {
                await Task.Delay(_firstWaitActualDelayMs, cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class HookedTimingService : IPlaybackTimingService
    {
        private int _waitCallCount;
        public List<int> WaitCalls { get; } = new();
        public Func<Task>? OnFirstWaitAsync { get; set; }

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            _waitCallCount++;

            if (_waitCallCount == 1 && OnFirstWaitAsync != null)
            {
                await OnFirstWaitAsync();
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }
}
