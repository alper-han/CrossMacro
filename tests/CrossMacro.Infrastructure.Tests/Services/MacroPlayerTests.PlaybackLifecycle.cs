// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class MacroPlayerTests
{

    [Fact]
    public async Task PlayAsync_NullMacro_ThrowsArgumentNullException()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        var act = async () => await player.PlayAsync(null!, cancellationToken: CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlayAsync_EmptyMacro_ThrowsInvalidOperationException()
    {
        // Arrange
        var player = CreatePlayer();
        var macro = new MacroSequence(); // Empty events

        // Act
        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public async Task PlayAsync_UsesInjectedPlaybackValidator()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var validator = Substitute.For<IPlaybackValidator>();
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(new PlaybackValidationResult());
        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        await player.PlayAsync(macro);

        TestAssertions.Verify(() => validator.Received(1).Validate(macro));
        simulator.Received().MoveRelative(10, 10);
    }

    [Fact]
    public async Task PlayAsync_WhenInjectedValidatorRejects_ThrowsInvalidOperationException()
    {
        var validator = Substitute.For<IPlaybackValidator>();
        var validationResult = new PlaybackValidationResult();
        validationResult.AddError("injected validation failure");
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(validationResult);
        var player = CreatePlayer(validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        async Task ActAsync() => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await TestAssertions.ThrowsWithMessageAsync<InvalidOperationException>(ActAsync, "*injected validation failure*");
        TestAssertions.Verify(() => validator.Received(1).Validate(macro));
    }

    [Fact]
    public async Task PlayAsync_WhenValidationFails_DoesNotAcquireResourcesOrChangeObservableState()
    {
        var validator = Substitute.For<IPlaybackValidator>();
        var validationResult = new PlaybackValidationResult();
        validationResult.AddError("validation failure");
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(validationResult);
        var factoryCalls = 0;
        var player = CreatePlayer(
            inputSimulatorFactory: () =>
            {
                factoryCalls++;
                return Substitute.For<IInputSimulator>();
            },
            validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = factoryCalls.Should().Be(0);
        _ = player.IsPlaying.Should().BeFalse();
        _ = player.CurrentLoop.Should().Be(0);
        _ = player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void IsPlaying_Initially_IsFalse()
    {
        // Arrange
        var player = CreatePlayer();

        // Assert
        _ = player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void IsPaused_Initially_IsFalse()
    {
        // Arrange
        var player = CreatePlayer();

        // Assert
        _ = player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void CurrentLoop_Initially_IsZero()
    {
        // Arrange
        var player = CreatePlayer();

        // Assert
        _ = player.CurrentLoop.Should().Be(0);
    }

    [Fact]
    public void TotalLoops_Initially_IsZero()
    {
        // Arrange
        var player = CreatePlayer();

        // Assert
        _ = player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        var act = () => player.StopPlayback();

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Pause_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.Pause();

        // Assert
        _ = player.IsPaused.Should().BeFalse(); // Can't pause when not playing
    }

    [Fact]
    public void Resume_WhenNotPlaying_DoesNothing()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.ResumePlayback();

        // Assert
        _ = player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        var act = () =>
        {
            player.Dispose();
            player.Dispose();
            player.Dispose();
        };

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task PlayAsync_ExecutesEvents_OnInputSimulator()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 100 },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left },
                new() { Type = EventType.KeyPress, KeyCode = 30 },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        // Verify MoveRelative (default mode)
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());

        // Verify MacroMouseButton
        simulator.Received().MouseButton(Arg.Any<int>(), pressed: true);

        // Verify KeyPress
        simulator.Received().KeyPress(30, pressed: true);
    }

    [Fact]
    public async Task PlayAsync_WhenWaitingBetweenLoops_ExposesCurrentLoopAndTotalLoops()
    {
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
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };
        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 123,
        };

        var playback = player.PlayAsync(macro, options);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout);

        _ = player.IsPlaying.Should().BeTrue();
        _ = player.CurrentLoop.Should().Be(1);
        _ = player.TotalLoops.Should().Be(2);
        _ = player.IsWaitingBetweenLoops.Should().BeTrue();

        _ = timing.ContinueWait.TrySetResult(true);
        await playback;

        _ = player.CurrentLoop.Should().Be(0);
        _ = player.TotalLoops.Should().Be(0);
        _ = player.IsWaitingBetweenLoops.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_WhenAlreadyPlaying_ThrowsInvalidOperationException()
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
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };

        // Block timing service so first playback remains in-progress.
        timing.WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        timing.ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstPlayback = player.PlayAsync(macro);
        _ = await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        var act = async () => await player.PlayAsync(macro);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");

        player.StopPlayback();
        _ = timing.ContinueWait.TrySetResult(true);
        await firstPlayback;
    }

    [Fact]
    public async Task PlayAsync_WhenCallerCancels_RethrowsCancellationAndCleansUpOnce()
    {
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
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };
        using var cancellation = new CancellationTokenSource();

        var playback = player.PlayAsync(macro, cancellationToken: cancellation.Token);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout);
        await cancellation.CancelAsync();

        var act = async () => await playback;
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
        _ = player.IsPlaying.Should().BeFalse();
        _ = player.CurrentLoop.Should().Be(0);
        _ = player.TotalLoops.Should().Be(0);
        simulator.Received(1).Dispose();
    }

    [Fact]
    public async Task PlayAsync_WhenStoppedInternally_CompletesWithoutThrowingAndReleasesResourcesOnce()
    {
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
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };

        var playback = player.PlayAsync(macro);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout);
        player.StopPlayback();

        await playback;
        _ = player.IsPlaying.Should().BeFalse();
        _ = player.CurrentLoop.Should().Be(0);
        _ = player.TotalLoops.Should().Be(0);
        simulator.Received(1).Dispose();
    }

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenEventsAndResumed_ExecutesAllEventsInOrder()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var secondEventStarted = new AsyncSignal();
        var pauseObserved = new AsyncSignal();
        var timing = new ControlledTimingService
        {
            OnWaitAsync = async (_, _, pauseToken, cancellationToken) =>
            {
                if (pauseToken.IsPaused)
                {
                    pauseObserved.Signal();
                    await pauseToken.WaitIfPausedAsync(cancellationToken);
                }
            },
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
            Events = {
                new() { Type = EventType.MouseMove, X = 10, Y = 10, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 30, Y = 30, DelayMs = 40 },
                new() { Type = EventType.MouseMove, X = 40, Y = 40, DelayMs = 40 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await pauseObserved.WaitAsync(TestTimeout);
        _ = player.IsPaused.Should().BeTrue();

        _ = playbackTask.IsCompleted.Should().BeFalse();

        player.ResumePlayback();
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

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenNonModifierKeyPressAndRelease_ShouldNotReEmitKeyPressOnResume()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex is 1)
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
            Events = {
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_A, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_A, DelayMs = 80 },
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_B, DelayMs = 80 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_B, DelayMs = 80 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await waitEntered.WaitAsync(TestTimeout);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout);
        player.ResumePlayback();
        await playbackTask;

        // Assert
        simulator.Received(1).KeyPress(InputEventCode.KEY_A, pressed: true);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenModifierKeyPressAndRelease_ShouldRestoreModifierOnResume()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            timingService: timing);

        timing.OnWaitAsync = async (callIndex, _, pauseToken, cancellationToken) =>
        {
            if (callIndex is 1)
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
            Events = {
                new() { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_LEFTCTRL, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_LEFTCTRL, DelayMs = 100 },
            },
        };

        // Act
        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 });
        await waitEntered.WaitAsync(TestTimeout);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout);
        player.ResumePlayback();
        await playbackTask;

        // Assert
        simulator.Received(2).KeyPress(InputEventCode.KEY_LEFTCTRL, pressed: true);
    }
}
