namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class MacroPlayerPlaybackLifecycleTests : MacroPlayerTestBase
{
    [Fact]
    public async Task PlayAsync_NullMacro_ThrowsArgumentNullException()
    {
        using var player = CreatePlayer();

        var act = async () => await player.PlayAsync(null!, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlayAsync_EmptyMacro_ThrowsInvalidOperationException()
    {
        using var player = CreatePlayer();
        var macro = new MacroSequence(); // Empty events

        var act = async () => await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

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
        using var player = CreatePlayer(
            inputSimulatorFactory: () => simulator,
            validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = validator.Received(1).Validate(macro);
        simulator.Received().MoveRelative(10, 10);
    }

    [Fact]
    public async Task PlayAsync_WhenInjectedValidatorRejects_ThrowsInvalidOperationException()
    {
        var validator = Substitute.For<IPlaybackValidator>();
        var validationResult = new PlaybackValidationResult();
        validationResult.AddError("injected validation failure");
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(validationResult);
        using var player = CreatePlayer(validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("injected validation failure", exception.Message, StringComparison.Ordinal);
        _ = validator.Received(1).Validate(macro);
    }

    [Fact]
    public async Task PlayAsync_WhenValidationFails_DoesNotAcquireResourcesOrChangeObservableState()
    {
        var validator = Substitute.For<IPlaybackValidator>();
        var validationResult = new PlaybackValidationResult();
        validationResult.AddError("validation failure");
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(validationResult);
        var factoryCalls = 0;
        using var player = CreatePlayer(
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

        var act = async () => await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = factoryCalls.Should().Be(0);
        _ = player.IsPlaying.Should().BeFalse();
        _ = player.CurrentLoop.Should().Be(0);
        _ = player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void IsPlaying_Initially_IsFalse()
    {
        using var player = CreatePlayer();

        _ = player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void IsPaused_Initially_IsFalse()
    {
        using var player = CreatePlayer();

        _ = player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void CurrentLoop_Initially_IsZero()
    {
        using var player = CreatePlayer();

        _ = player.CurrentLoop.Should().Be(0);
    }

    [Fact]
    public void TotalLoops_Initially_IsZero()
    {
        using var player = CreatePlayer();

        _ = player.TotalLoops.Should().Be(0);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        using var player = CreatePlayer();

        var act = () => player.StopPlayback();

        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Pause_WhenNotPlaying_DoesNothing()
    {
        using var player = CreatePlayer();

        player.Pause();

        _ = player.IsPaused.Should().BeFalse(); // Can't pause when not playing
    }

    [Fact]
    public void Resume_WhenNotPlaying_DoesNothing()
    {
        using var player = CreatePlayer();

        player.ResumePlayback();

        _ = player.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        using var player = CreatePlayer();

        var act = () =>
        {
            player.Dispose();
            player.Dispose();
            player.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task PlayAsync_ExecutesEvents_OnInputSimulator()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");

        using var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 100 },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left },
                new() { Type = EventType.KeyPress, KeyCode = 30 },
            },
        };

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        // Verify MoveRelative (default mode)
        simulator.Received().MoveRelative(Arg.Any<int>(), Arg.Any<int>());

        // Verify MacroMouseButton
        simulator.Received().MouseButton(Arg.Any<int>(), pressed: true);

        // Verify KeyPress
        simulator.Received().KeyPress(30, pressed: true);
    }

    [Fact]
    public async Task PlayAsync_WhenInputInjectionFails_StopsImmediately()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        simulator
            .When(input => input.MoveRelative(Arg.Any<int>(), Arg.Any<int>()))
            .Do(_ => throw new InputInjectionFailedException("native failure", nativeErrorCode: 5));
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            SkipInitialZeroZero = true,
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20 },
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InputInjectionFailedException>();
        simulator.Received(1).MoveRelative(10, 10);
        simulator.DidNotReceive().MoveRelative(20, 20);
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
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
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

        var playback = player.PlayAsync(macro, options, cancellationToken: TestContext.Current.CancellationToken);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);

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
    public async Task StateChanged_WhenLoopedPlaybackCompletes_ReportsEachTransitionOnce()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService
        {
            WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
        var states = new List<PlayerState>();
        player.StateChanged += (_, _) => states.Add(PlayerState.Capture(player));
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };
        var options = new PlaybackOptions { Loop = true, RepeatCount = 2, RepeatDelayMs = 123 };

        var playback = player.PlayAsync(macro, options, cancellationToken: TestContext.Current.CancellationToken);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = timing.ContinueWait.TrySetResult(true);
        await playback;

        _ = states.Should().Equal(
            new PlayerState(IsPlaying: true, IsPaused: false, CurrentLoop: 0, TotalLoops: 0, IsWaitingBetweenLoops: false),
            new PlayerState(IsPlaying: true, IsPaused: false, CurrentLoop: 1, TotalLoops: 2, IsWaitingBetweenLoops: false),
            new PlayerState(IsPlaying: true, IsPaused: false, CurrentLoop: 1, TotalLoops: 2, IsWaitingBetweenLoops: true),
            new PlayerState(IsPlaying: true, IsPaused: false, CurrentLoop: 1, TotalLoops: 2, IsWaitingBetweenLoops: false),
            new PlayerState(IsPlaying: true, IsPaused: false, CurrentLoop: 2, TotalLoops: 2, IsWaitingBetweenLoops: false),
            new PlayerState(IsPlaying: false, IsPaused: false, CurrentLoop: 0, TotalLoops: 0, IsWaitingBetweenLoops: false));
    }

    [Fact]
    public async Task StateChanged_WhenPausedResumedAndStopped_ReportsEachTransition()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService
        {
            WaitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            ContinueWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
        var states = new List<PlayerState>();
        player.StateChanged += (_, _) => states.Add(PlayerState.Capture(player));
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };

        var playback = player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);
        player.Pause();
        player.Pause();
        player.ResumePlayback();
        player.ResumePlayback();
        player.StopPlayback();
        _ = timing.ContinueWait.TrySetResult(true);
        await playback;

        _ = states.Select(state => (state.IsPlaying, state.IsPaused)).Should().Equal(
            (true, false),
            (true, false),
            (true, true),
            (true, false),
            (false, false));
    }

    [Fact]
    public async Task StateChanged_WhenPlaybackFails_ReportsEndOfRun()
    {
        var validator = Substitute.For<IPlaybackValidator>();
        var validationResult = new PlaybackValidationResult();
        validationResult.AddError("validation failure");
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(validationResult);
        using var player = CreatePlayer(validator: validator);
        var states = new List<PlayerState>();
        player.StateChanged += (_, _) => states.Add(PlayerState.Capture(player));
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = states.Select(state => state.IsPlaying).Should().Equal(true, false);
    }

    private readonly record struct PlayerState(bool IsPlaying, bool IsPaused, int CurrentLoop, int TotalLoops, bool IsWaitingBetweenLoops)
    {
        public static PlayerState Capture(IMacroPlayer player) =>
            new(player.IsPlaying, player.IsPaused, player.CurrentLoop, player.TotalLoops, player.IsWaitingBetweenLoops);
    }

    [Fact]
    public async Task PlayAsync_WhenAlreadyPlaying_ThrowsInvalidOperationException()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var timing = new RecordingTimingService();
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);

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

        var firstPlayback = player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);
        _ = await timing.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        var act = async () => await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<PlaybackAlreadyInProgressException>();

        player.StopPlayback();
        _ = timing.ContinueWait.TrySetResult(true);
        await firstPlayback;
    }

    [Fact]
    public async Task DisposeAsync_WhilePlaying_StopsPlaybackAndWaitsForItBeforeReleasingSimulator()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timing = new ControlledTimingService
        {
            // Deliberately ignores cancellation: the run only ends once the wait is released.
            OnWaitAsync = async (_, _, _, _) =>
            {
                _ = waitEntered.TrySetResult();
                await releaseWait.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
            },
        };
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };
        var cancellationToken = TestContext.Current.CancellationToken;
        var playback = player.PlayAsync(macro, cancellationToken: cancellationToken);
        await waitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, cancellationToken);

        var disposal = player.DisposeAsync().AsTask();
        _ = disposal.IsCompleted.Should().BeFalse();
        simulator.DidNotReceive().Dispose();
        _ = releaseWait.TrySetResult();
        await disposal.WaitAsync(TestTimeout, TimeProvider.System, cancellationToken);
        await playback.WaitAsync(TestTimeout, TimeProvider.System, cancellationToken);

        _ = player.IsPlaying.Should().BeFalse();
        simulator.Received(1).Dispose();
    }

    [Fact]
    public async Task PlayAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using var player = CreatePlayer();
        player.Dispose();

        var act = async () => await player.PlayAsync(
            new MacroSequence { Events = { new() { Type = EventType.MouseMove, X = 1, Y = 1 } } },
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PlayAsync_WhenConcurrentCallsRaceBeforeBegin_AllowsOnlyOnePlayback()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var validator = Substitute.For<IPlaybackValidator>();
        var validationEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = validator.Validate(Arg.Any<MacroSequence>()).Returns(_ =>
        {
            validationEntered.TrySetResult(true);
            releaseValidation.Task.GetAwaiter().GetResult();
            return new PlaybackValidationResult();
        });
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, validator: validator);
        var macro = new MacroSequence
        {
            Events = { new() { Type = EventType.MouseMove, X = 10, Y = 10 } },
        };

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstPlayback = Task.Run(
            () => player.PlayAsync(macro, cancellationToken: cancellationToken),
            cancellationToken);
        await validationEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, cancellationToken);

        var act = async () => await player.PlayAsync(macro, cancellationToken: cancellationToken);

        _ = await act.Should().ThrowAsync<PlaybackAlreadyInProgressException>();

        _ = releaseValidation.TrySetResult(true);
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
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
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
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, cancellation.Token);
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
        using var player = CreatePlayer(inputSimulatorFactory: () => simulator, timingService: timing);
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 10 },
                new() { Type = EventType.MouseMove, X = 20, Y = 20, DelayMs = 40 },
            },
        };

        var playback = player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);
        _ = await timing.WaitEntered.Task.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);
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

        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 }, cancellationToken: TestContext.Current.CancellationToken);
        await pauseObserved.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        _ = player.IsPaused.Should().BeTrue();

        _ = playbackTask.IsCompleted.Should().BeFalse();

        player.ResumePlayback();
        await playbackTask;

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
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        using var player = CreatePlayer(
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

        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 }, cancellationToken: TestContext.Current.CancellationToken);
        await waitEntered.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        player.ResumePlayback();
        await playbackTask;

        simulator.Received(1).KeyPress(InputEventCode.KEY_A, pressed: true);
    }

    [Fact]
    public async Task PlayAsync_WhenPausedBetweenModifierKeyPressAndRelease_ShouldRestoreModifierOnResume()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.ProviderName.Returns("MockSimulator");
        var waitEntered = new AsyncSignal();
        var releaseWait = new AsyncSignal();
        var paused = new AsyncSignal();
        var timing = new ControlledTimingService();

        using var player = CreatePlayer(
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

        var playbackTask = player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 1.0 }, cancellationToken: TestContext.Current.CancellationToken);
        await waitEntered.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        player.Pause();
        releaseWait.Signal();
        await paused.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
        player.ResumePlayback();
        await playbackTask;

        simulator.Received(2).KeyPress(InputEventCode.KEY_LEFTCTRL, pressed: true);
    }
}
