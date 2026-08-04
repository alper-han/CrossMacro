namespace CrossMacro.Core.Tests.Services;


/// <summary>
/// Tests for MacroPlayer focusing on edge cases and error handling
/// </summary>
public sealed class MacroPlayerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);
    private readonly IMousePositionProvider _positionProvider;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly PlaybackValidator _validator;

    public MacroPlayerTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _ = _positionProvider.IsSupported.Returns(returnThis: true);
        _ = _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _keyCodeMapper = CreateKeyCodeMapper();
        _validator = new PlaybackValidator(_keyCodeMapper, _positionProvider);
    }

    private MacroPlayer CreatePlayer(
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null,
        IPlaybackValidator? validator = null)
    {
        return new MacroPlayer(validator ?? _validator, CreateDependencies(
            _positionProvider,
            inputSimulatorFactory,
            timingService,
            playbackWaitAsync ?? ((_, _) => Task.CompletedTask),
            playbackElapsedMillisecondsFactory,
            _keyCodeMapper));
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider? positionProvider,
        Func<IInputSimulator>? inputSimulatorFactory,
        IPlaybackTimingService? timingService,
        Func<TimeSpan, CancellationToken, Task> playbackWaitAsync,
        Func<Func<double>>? playbackElapsedMillisecondsFactory,
        IKeyCodeMapper keyCodeMapper,
        IScreenPixelReader? screenPixelReader = null,
        IClipboardService? clipboardService = null,
        IShellCommandRunner? shellCommandRunner = null,
        IScreenshotCaptureService? screenshotCaptureService = null)
    {
        return new MacroPlayerDependencies(
            positionProvider,
            timingService ?? new PlaybackTimingService(),
            playbackWaitAsync,
            playbackElapsedMillisecondsFactory ?? CreateElapsedMillisecondsProvider,
            () => new DefaultPlaybackCoordinator(positionProvider),
            () => new ButtonStateTracker(),
            () => new KeyStateTracker(),
            new DefaultPlaybackMouseButtonMapper(),
            inputSimulatorFactory,
            simulatorPool: null,
            screenPixelReader ?? NullScreenPixelReader.Instance,
            keyCodeMapper,
            new NullWindowManager(),
            clipboardService,
            shellCommandRunner,
            screenshotCaptureService,
            new ImageClickMovementResolver(positionProvider),
            new ImageAssetCodec(),
            new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }

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
    public async Task PlayAsync_WhenSpeedProducesSubMillisecondDelay_PreservesFractionalWait()
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
                new() { Type = EventType.MouseMove, X = 2, Y = 2, DelayMs = 1 },
            },
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 2.0 });

        _ = timing.WaitCalls.Should().ContainSingle();
        _ = timing.WaitCalls[0].Should().BeApproximately(0.5, 0.001);
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
    public async Task PlayAsync_WhenHighSpeedPlaybackHasSmallDrift_ShouldPreserveCatchUpPacing()
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
        _ = timing.WaitCalls[0].Should().BeApproximately(100d / 3d, 0.000_001);
        _ = timing.WaitCalls[1].Should().BeApproximately((100d / 3d) - 12d, 0.000_001);
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

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClick_UsesLiveCursorWithoutSyntheticMove()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.ButtonTransitions.Should().HaveCount(2);
        _ = simulator.Operations[0].Should().Be("btn:down");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickLoops_DoesNotInjectSyntheticMovement()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true },
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
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionEventHasStoredCoordinates_DoesNotMoveToStoredPosition()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 999,
                    Y = 777,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenMacroHasMixedCoordinateModes_ExecutesEachEventWithEffectiveMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().ContainInOrder("abs:100,200", "rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenMacroCombinesCurrentAbsoluteAndRelativeEvents_UsesPerEventMovementSemantics()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Right,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Equal(
            "btn:down",
            "btn:up",
            "abs:100,200",
            "rel:10,-5",
            "btn:down",
            "btn:up");
        _ = simulator.AbsoluteMoves.Should().Equal((100, 200));
        _ = simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenLegacyAbsoluteMacroHasExplicitRelativeEvent_UsesRelativeEventMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.Operations.Should().Contain("rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesResolutionOnlyProvider_CachesResolutionAndCreatesAbsoluteDevice()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("Niri IPC (Resolution Only)");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));

        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(resolutionOnlyProvider, () => simulator, timingService: null, (_, _) => Task.CompletedTask, playbackElapsedMillisecondsFactory: null, _keyCodeMapper));

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Contain("abs:100,200");
    }

    [Fact]
    public async Task PlayAsync_WhenDesktopTopologyChangesBetweenRuns_RefreshesAbsoluteDeviceBounds()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.ProviderName.Returns("Mutable desktop");
        _ = positionProvider.GetAbsolutePositionAsync()
            .Returns(Task.FromResult<(int X, int Y)?>((0, 0)));
        var currentBounds = new ScreenRect(0, 0, 1920, 1080);
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(_ => Task.FromResult<ScreenRect?>(currentBounds));

        var firstSimulator = new TrackingInputSimulator();
        var secondSimulator = new TrackingInputSimulator();
        var simulators = new Queue<TrackingInputSimulator>([firstSimulator, secondSimulator]);
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, positionProvider),
            CreateDependencies(
                positionProvider,
                () => simulators.Dequeue(),
                timingService: null,
                (_, _) => Task.CompletedTask,
                playbackElapsedMillisecondsFactory: null,
                _keyCodeMapper));
        var macro = new MacroSequence
        {
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await player.PlayAsync(macro);
        currentBounds = new ScreenRect(-2560, -400, 6400, 2560);
        await player.PlayAsync(macro);

        Assert.Equal((1920, 1080), (firstSimulator.InitializedWidth, firstSimulator.InitializedHeight));
        Assert.Equal((6400, 2560), (secondSimulator.InitializedWidth, secondSimulator.InitializedHeight));
        _ = await positionProvider.Received(2).GetDesktopBoundsAsync();
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesResolutionOnlyProvider_PreparesCornerResetAndPlaysRelativeOnly()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("COSMIC RandR (Resolution Only)");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((2560, 1440)));

        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(resolutionOnlyProvider, () => simulator, timingService: null, (_, _) => Task.CompletedTask, playbackElapsedMillisecondsFactory: null, _keyCodeMapper));

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        _ = simulator.InitializedWidth.Should().Be(2560);
        _ = simulator.InitializedHeight.Should().Be(1440);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.Operations.Should().Contain("rel:-20000,0");
        _ = simulator.Operations.Should().Contain("rel:0,-20000");
        _ = simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenMixedMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativeEventHasKnownPosition_UsesAbsoluteLogicalTarget()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync()
            .Returns(Task.FromResult<(int X, int Y)?>((100, 200)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Contain("abs:103,195");
        _ = simulator.Operations.Should().NotContain("rel:3,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativeEventUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativePositionCannotBeAnchored_ThrowsWithoutRawFallback()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.SupportsAbsolutePosition.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("Resolution Only");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync()
            .Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(
                resolutionOnlyProvider,
                () => simulator,
                timingService: null,
                (_, _) => Task.CompletedTask,
                playbackElapsedMillisecondsFactory: null,
                _keyCodeMapper));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<LogicalRelativePositionUnavailableException>();
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMoveAnchorsLogicalRelativeMove_PreservesMixedPath()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("abs:100,200", "abs:103,195");
    }

    [Fact]
    public async Task PlayAsync_WhenRawMovePrecedesLogicalRelativeMove_AnchorsToObservedCursorPosition()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((100, 100)),
            Task.FromResult<(int X, int Y)?>((100, 100)),
            Task.FromResult<(int X, int Y)?>((120, 110)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 5,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -2,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("rel:5,5", "abs:123,108");
        _ = await _positionProvider.Received(3).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesLegacyRelativeMove_DoesNotRequireAbsoluteDevice()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "set dx 3", "move rel $dx -5" },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.Operations.Should().ContainSingle().Which.Should().Be("rel:3,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesLogicalRelativeMove_UsesAbsoluteLogicalTarget()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync()
            .Returns(Task.FromResult<(int X, int Y)?>((100, 200)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "set dx 3", "move rel-logical $dx -5" },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().ContainSingle().Which.Should().Be("abs:103,195");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                    X = 100,
                    Y = 200,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("btn:down", "btn:up");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteThenRelativeMacroPlays_ExecutesExactMovementSequence()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 1000,
                    Y = 1000,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("abs:1000,1000", "rel:3,3");
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

    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<double> WaitCalls { get; } = new();
        public TaskCompletionSource<bool>? WaitEntered { get; set; }
        public TaskCompletionSource<bool>? ContinueWait { get; set; }

        public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMilliseconds);
            _ = (WaitEntered?.TrySetResult(true));

            if (ContinueWait is not null)
            {
                _ = await ContinueWait.Task.WaitAsync(cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class ControlledTimingService : IPlaybackTimingService
    {
        public List<double> WaitCalls { get; } = new();
        public Func<int, double, IPlaybackPauseToken, CancellationToken, Task>? OnWaitAsync { get; set; }
        private int _waitCallCount;

        public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMilliseconds);
            int callIndex = ++_waitCallCount;
            if (OnWaitAsync is not null)
            {
                await OnWaitAsync(callIndex, delayMilliseconds, pauseToken, cancellationToken);
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

    private sealed class TrackingInputSimulator(bool forceRelativeOnly = false) : IInputSimulator, IInputSimulatorCapabilities
    {
        public string ProviderName => "Tracking";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates { get => !field && InitializedWidth > 0 && InitializedHeight > 0; } = forceRelativeOnly;
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

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
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
