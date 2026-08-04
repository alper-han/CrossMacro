namespace CrossMacro.Core.Tests.Services;


/// <summary>
/// Tests for MacroRecorder focusing on initialization and error handling
/// </summary>
public sealed class MacroRecorderTests
{
    private readonly Func<IInputCapture> _captureFactory;
    private readonly ICoordinateStrategyFactory _strategyFactory;
    private readonly Func<ICoordinateStrategy, IInputEventProcessor> _processorFactory;

    // Mocks returned by factories
    private readonly IInputCapture _capture;
    private readonly ICoordinateStrategy _strategy;
    private readonly IInputEventProcessor _processor;

    public MacroRecorderTests()
    {
        _capture = Substitute.For<IInputCapture>();
        _captureFactory = () => _capture;

        _strategy = Substitute.For<ICoordinateStrategy>();
        _strategyFactory = Substitute.For<ICoordinateStrategyFactory>();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(_strategy);

        _processor = Substitute.For<IInputEventProcessor>();
        _processorFactory = (s) => _processor;
    }

    private MacroRecorder CreateRecorder()
    {
        return new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => Substitute.For<IInputSimulator>());
    }

    [Fact]
    public void IsRecording_Initially_IsFalse()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Assert
        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_NoMouseNoKeyboard_ThrowsArgumentException()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        var act = async () => await recorder.StartRecordingAsync(
            recordMouse: false,
            recordKeyboard: false,
            cancellationToken: CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void StopRecording_WhenNotRecording_ThrowsInvalidOperationException()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        var act = () => recorder.StopRecording();

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Not currently recording*");
    }

    [Fact]
    public void GetCurrentRecording_WhenNotRecording_ReturnsNull()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        var result = recorder.GetCurrentRecording();

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        var act = () =>
        {
            recorder.Dispose();
            recorder.Dispose();
        };

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task StartRecordingAsync_CapturesEvents_WhenInputReceived()
    {
        // Arrange


        var recorder = CreateRecorder();

        var receivedEvents = new List<MacroEvent>();
        recorder.EventRecorded += (s, e) => receivedEvents.Add(e.MacroEvent);

        // Setup processor to return an event when Process is called
        _ = _processor.Process(Arg.Any<CapturedInputEvent>(), Arg.Any<long>())
            .Returns(new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 });

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);

        // Simulate input
        _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
        this,
        new CapturedInputEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 });

        _ = recorder.StopRecording();

        // Assert
        _ = receivedEvents.Should().HaveCount(1);
        _ = receivedEvents[0].Type.Should().Be(EventType.KeyPress);
        _ = receivedEvents[0].KeyCode.Should().Be(30);
    }

    [Fact]
    public async Task StartRecordingAsync_InitializesStrategyAndProcessor()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);

        // Assert
        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyStaysAbsolute_UsesAbsoluteModeForProcessorAndSequence()
    {
        // Arrange
        var absoluteStrategy = Substitute.For<ICoordinateStrategy>();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(absoluteStrategy);
        var recorder = CreateRecorder();

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false);
        var sequence = recorder.StopRecording();

        // Assert
        await absoluteStrategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: true);
        _ = sequence.IsAbsoluteCoordinates.Should().BeTrue();
    }
    [Fact]
    public async Task StartRecordingAsync_WithForceRelative_PerformsCornerReset()
    {
        // Arrange
        var mockSimulator = Substitute.For<IInputSimulator>();
        var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: true, skipInitialZero: false);

        // Assert
        // Verify Corner Reset fallback keeps axes separate so monitor edges do not trap a diagonal move.
        await mockSimulator.Received(1).InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        mockSimulator.Received(2).MoveRelative(-20000, 0);
        mockSimulator.Received(2).MoveRelative(0, -20000);
        mockSimulator.DidNotReceive().MoveRelative(-20000, -20000);

        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRecordingAsync_WithDesktopBoundsAndAbsoluteSimulator_ResetsToZeroBasedDesktopOrigin()
    {
        var mockSimulator = Substitute.For<
            IInputSimulator,
            IInputSimulatorCapabilities,
            IInputSimulatorAbsoluteBounds>();
        _ = ((IInputSimulatorCapabilities)mockSimulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)mockSimulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(Task.FromResult<ScreenRect?>(new ScreenRect(-1920, -200, 4480, 1640)));
        var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => mockSimulator,
            positionProvider);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false);

        await mockSimulator.Received(1).InitializeAsync(4480, 1640, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            mockSimulator.MoveAbsolute(1, 1);
            mockSimulator.MoveAbsolute(0, 0);
        });
        mockSimulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());

        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRecordingAsync_WithGlobalAbsoluteSimulator_PreservesDesktopOrigin()
    {
        var mockSimulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)mockSimulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(Task.FromResult<ScreenRect?>(new ScreenRect(-1920, -200, 4480, 1640)));
        var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => mockSimulator,
            positionProvider);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false);

        mockSimulator.Received(1).MoveAbsolute(-1920, -200);
        mockSimulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureCompletesAfterStop_DoesNotThrow()
    {
        // Arrange
        var captureRunTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _capture.ProviderName.Returns("TestCapture");
        _ = _capture.StartAsync(Arg.Any<CancellationToken>()).Returns(captureRunTcs.Task);

        var recorder = CreateRecorder();

        // Act
        var startTask = recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);
        await Task.Yield();
        var stopResult = recorder.StopRecording();
        captureRunTcs.SetResult();

        // Assert
        _ = stopResult.Should().NotBeNull();
        Func<Task> act = async () => await startTask;
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyFallsBackToRelative_UsesRelativeModeForProcessorAndSequence()
    {
        // Arrange
        var relativeStrategy = new RelativeCoordinateStrategy();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false);
        var sequence = recorder.StopRecording();

        // Assert
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: false);
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        await mockSimulator.Received(1).InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        mockSimulator.Received(2).MoveRelative(-20000, 0);
        mockSimulator.Received(2).MoveRelative(0, -20000);
    }

    [Fact]
    public async Task StartRecordingAsync_ShouldConfigureModeAwareCaptureWithEffectiveStrategyMode()
    {
        var modeAwareCapture = Substitute.For<IInputCapture, IMouseCoordinateModeInputCapture>();
        var relativeStrategy = new RelativeCoordinateStrategy();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var recorder = new MacroRecorder(
            () => modeAwareCapture,
            _strategyFactory,
            _processorFactory);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: false,
            skipInitialZero: true);
        _ = recorder.StopRecording();

        ((IMouseCoordinateModeInputCapture)modeAwareCapture)
            .Received(1)
            .ConfigureCoordinateMode(
                useAbsoluteCoordinates: false,
                useLogicalCoordinates: false);
    }

    [Fact]
    public async Task StartRecordingAsync_ShouldForwardLogicalRelativeSemanticsToModeAwareCapture()
    {
        var modeAwareCapture = Substitute.For<IInputCapture, IMouseCoordinateModeInputCapture>();
        var relativeStrategy = Substitute.For<IRelativeCoordinateStrategy>();
        _ = relativeStrategy.ProducesRelativeCoordinates.Returns(returnThis: true);
        _ = relativeStrategy.ProducesLogicalCoordinates.Returns(returnThis: true);
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var recorder = new MacroRecorder(
            () => modeAwareCapture,
            _strategyFactory,
            _processorFactory);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: true);
        _ = recorder.StopRecording();

        ((IMouseCoordinateModeInputCapture)modeAwareCapture)
            .Received(1)
            .ConfigureCoordinateMode(
                useAbsoluteCoordinates: false,
                useLogicalCoordinates: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenPlatformSpecificRelativeStrategyUsed_UsesRelativeModeForProcessorAndSequence()
    {
        // Arrange
        var relativeStrategy = Substitute.For<IRelativeCoordinateStrategy>();
        _ = relativeStrategy.ProducesRelativeCoordinates.Returns(returnThis: true);
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false);
        var sequence = recorder.StopRecording();

        // Assert
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: false);
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        await mockSimulator.Received(1).InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        mockSimulator.Received(2).MoveRelative(-20000, 0);
        mockSimulator.Received(2).MoveRelative(0, -20000);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyFallsBackToRelative_AndSkipInitialZero_LeavesCursorAsIs()
    {
        // Arrange
        var relativeStrategy = new RelativeCoordinateStrategy();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        // Act
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false, skipInitialZero: true);
        var sequence = recorder.StopRecording();

        // Assert
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: false);
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        mockSimulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenInputCaptureFactoryMissing_ThrowsAndResetsState()
    {
        // Arrange
        var recorder = new MacroRecorder(inputCaptureFactory: null, _strategyFactory, _processorFactory);

        // Act
        var act = async () => await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No input capture factory configured*");
        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureStartThrows_CleansUpAndRethrows()
    {
        // Arrange
        _ = _capture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("start failed")));
        var recorder = CreateRecorder();

        // Act
        var act = async () => await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("start failed");
        _ = recorder.IsRecording.Should().BeFalse();
        _capture.Received(1).StopCapture();
        _capture.Received(1).Dispose();
    }

    [Fact]
    public async Task StopRecording_WhenCaptureStopThrows_ReturnsRecordedMacro()
    {
        // Arrange
        var recorder = CreateRecorder();
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);
        _capture.When(x => x.StopCapture()).Do(_ => throw new InvalidOperationException("stop fail"));

        // Act
        var result = recorder.StopRecording();

        // Assert
        _ = result.Should().NotBeNull();
        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StopRecording_WhenInputCallbackIsInFlight_WaitsForItsEventBeforeFinalizing()
    {
        var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _processor.Process(Arg.Any<CapturedInputEvent>(), Arg.Any<long>()).Returns(callInfo =>
        {
            _ = processingStarted.TrySetResult();
            releaseProcessing.Task.GetAwaiter().GetResult();
            return new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 };
        });
        var recorder = CreateRecorder();
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true);

        var inputTask = Task.Run(() =>
            _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
                this,
                new CapturedInputEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 }));

        await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        var stopTask = Task.Run(recorder.StopRecording);

        try
        {
            await Task.Delay(25, CancellationToken.None);
            _ = stopTask.IsCompleted.Should().BeFalse();
        }
        finally
        {
            _ = releaseProcessing.TrySetResult();
        }

        var sequence = await stopTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
        await inputTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
    }
}
