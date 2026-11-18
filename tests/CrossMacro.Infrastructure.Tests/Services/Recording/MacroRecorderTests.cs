using CrossMacro.Infrastructure.Services.Recording;
using CrossMacro.Infrastructure.Services.Recording.Processors;
using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Infrastructure.Tests.Services.Recording;


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
        _capture = Substitute.For<IInputCapture, IMouseCoordinateModeInputCapture>();
        _captureFactory = () => _capture;

        _strategy = Substitute.For<ICoordinateStrategy>();
        _strategyFactory = Substitute.For<ICoordinateStrategyFactory>();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(_strategy);

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
        using var recorder = CreateRecorder();

        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_NoMouseNoKeyboard_ThrowsArgumentException()
    {
        using var recorder = CreateRecorder();

        var act = async () => await recorder.StartRecordingAsync(
            recordMouse: false,
            recordKeyboard: false,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void StopRecording_WhenNotRecording_ThrowsInvalidOperationException()
    {
        using var recorder = CreateRecorder();

        var act = () => recorder.StopRecording();

        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Not currently recording*");
    }

    [Fact]
    public void GetCurrentRecording_WhenNotRecording_ReturnsNull()
    {
        using var recorder = CreateRecorder();

        var result = recorder.GetCurrentRecording();

        _ = result.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        using var recorder = CreateRecorder();

        var act = () =>
        {
            recorder.Dispose();
            recorder.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task StartRecordingAsync_CapturesEvents_WhenInputReceived()
    {

        using var recorder = CreateRecorder();

        var receivedEvents = new List<MacroEvent>();
        recorder.EventRecorded += (s, e) => receivedEvents.Add(e.MacroEvent);

        // Setup processor to return an event when Process is called
        _ = _processor.Process(Arg.Any<CapturedInputEvent>(), Arg.Any<long>())
            .Returns(new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 });

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);

        // Simulate input
        _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
        this,
        new CapturedInputEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 });

        _ = recorder.StopRecording();

        _ = receivedEvents.Should().HaveCount(1);
        _ = receivedEvents[0].Type.Should().Be(EventType.KeyPress);
        _ = receivedEvents[0].KeyCode.Should().Be(30);
    }

    [Fact]
    public async Task StartRecordingAsync_UsesMonotonicSourceTimestampsInsteadOfArrivalTiming()
    {
        using var recorder = CreateRecorder();
        _ = _processor.Process(Arg.Any<CapturedInputEvent>(), Arg.Any<long>())
            .Returns(new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 });

        await recorder.StartRecordingAsync(recordMouse: false, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);
        _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs
            {
                Type = InputEventType.Key,
                Code = 30,
                Value = 1,
                TimestampMicroseconds = 1_000_000,
            });
        _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs
            {
                Type = InputEventType.Key,
                Code = 30,
                Value = 0,
                TimestampMicroseconds = 1_002_375,
            });

        var sequence = recorder.StopRecording();

        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[1].TimestampMicroseconds.Should().Be(sequence.Events[0].TimestampMicroseconds + 2_375);
        _ = sequence.Events[1].DelayMicroseconds.Should().Be(2_375);
    }

    [Fact]
    public async Task StartRecordingAsync_AbsoluteCoordinates_ClampsRecordedCoordinatesToDesktopBounds()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(Task.FromResult<ScreenRect?>(new ScreenRect(0, 0, 100, 80)));
        _ = _processor.Process(Arg.Any<CapturedInputEvent>(), Arg.Any<long>())
            .Returns(new MacroEvent
            {
                Type = EventType.MouseMove,
                X = 150,
                Y = -20,
                Timestamp = 10,
                CoordinateMode = MouseCoordinateMode.Absolute,
                CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
            });
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            positionProvider: positionProvider);
        var publishedEvents = new List<MacroEvent>();
        recorder.EventRecorded += (_, args) => publishedEvents.Add(args.MacroEvent);

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);
        _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.MouseMove });
        var sequence = recorder.StopRecording();

        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].X.Should().Be(99);
        _ = sequence.Events[0].Y.Should().Be(0);
        _ = publishedEvents.Should().ContainSingle();
        _ = publishedEvents[0].X.Should().Be(99);
        _ = publishedEvents[0].Y.Should().Be(0);
        _ = await positionProvider.Received(1).GetDesktopBoundsAsync();
    }

    [Fact]
    public async Task StartRecordingAsync_InitializesStrategyAndProcessor()
    {
        using var recorder = CreateRecorder();

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);

        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyStaysAbsolute_UsesAbsoluteModeForProcessorAndSequence()
    {
        var absoluteStrategy = Substitute.For<ICoordinateStrategy>();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(absoluteStrategy);
        using var recorder = CreateRecorder();

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false, cancellationToken: TestContext.Current.CancellationToken);
        var sequence = recorder.StopRecording();

        await absoluteStrategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: true);
        _ = sequence.IsAbsoluteCoordinates.Should().BeTrue();
    }
    [Fact]
    public async Task StartRecordingAsync_WithForceRelative_PerformsCornerReset()
    {
        var mockSimulator = Substitute.For<IInputSimulator>();
        using var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: true, skipInitialZero: false, cancellationToken: TestContext.Current.CancellationToken);

        // Verify Corner Reset fallback keeps axes separate so monitor edges do not trap a diagonal move.
        await mockSimulator.Received(1).InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        mockSimulator.Received(2).MoveRelative(-20000, 0);
        mockSimulator.Received(2).MoveRelative(0, -20000);
        mockSimulator.DidNotReceive().MoveRelative(-20000, -20000);

        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRecordingAsync_WithLogicalRelative_UsesLogicalStrategy()
    {
        _ = _strategy.ProducesLogicalCoordinates.Returns(returnThis: true);
        var modeAwareCapture = (IMouseCoordinateModeInputCapture)_capture;
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => Substitute.For<IInputSimulator>(),
            positionProvider);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            ignoredKeys: null,
            forceRelative: true,
            skipInitialZero: true,
            useLogicalRelativeCoordinates: true,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = _strategyFactory.Received(1).Create(
            useAbsoluteCoordinates: false,
            forceRelative: true,
            skipInitialZero: true,
            useLogicalRelativeCoordinates: true);
        modeAwareCapture.Received(1).ConfigureCoordinateMode(
            useAbsoluteCoordinates: false,
            useLogicalCoordinates: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WithLogicalRelativeWithoutCursorPosition_ThrowsBeforeCaptureStarts()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: false);
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => Substitute.For<IInputSimulator>(),
            positionProvider);

        var action = async () => await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: false,
            ignoredKeys: null,
            forceRelative: true,
            skipInitialZero: true,
            useLogicalRelativeCoordinates: true,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires access to the global cursor position*");
        _capture.DidNotReceive().Configure(Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenLogicalStrategyIsUnavailable_ThrowsWithoutRawFallback()
    {
        _ = _strategy.ProducesLogicalCoordinates.Returns(returnThis: false);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => Substitute.For<IInputSimulator>(),
            positionProvider);

        var action = async () => await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: false,
            ignoredKeys: null,
            forceRelative: true,
            skipInitialZero: true,
            useLogicalRelativeCoordinates: true,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not supported by the active input backend*");
        _capture.DidNotReceive().Configure(Arg.Any<bool>(), Arg.Any<bool>());
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
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => mockSimulator,
            positionProvider);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false,
            cancellationToken: TestContext.Current.CancellationToken);

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
    public async Task StartRecordingAsync_WithForceRelative_UsesAndReturnsPooledSimulator()
    {
        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities, IInputSimulatorAbsoluteBounds>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(4480, 1640, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IInputSimulator>(simulator));
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(new ScreenRect(-1920, -200, 4480, 1640)));
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(-1920, -200)));
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            inputSimulatorFactory: () => throw new InvalidOperationException("The pool should be used."),
            positionProvider: positionProvider,
            inputSimulatorPool: pool);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await pool.Received(1).AcquireAsync(4480, 1640, Arg.Any<CancellationToken>());
        pool.Received(1).Release(simulator);
        _ = simulator.DidNotReceive().InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        _ = await positionProvider.Received(1).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task StartRecordingAsync_WithForceRelative_UsesDesktopOriginResetCapabilityFromPooledSimulator()
    {
        var simulator = Substitute.For<IInputSimulator, IDesktopOriginResetSimulator>();
        var originReset = (IDesktopOriginResetSimulator)simulator;
        _ = originReset.TryResetToDesktopOrigin().Returns(returnThis: true);
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(3840, 1080, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IInputSimulator>(simulator));
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(new ScreenRect(0, 0, 3840, 1080)));
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            inputSimulatorFactory: () => throw new InvalidOperationException("The pool should be used."),
            positionProvider: positionProvider,
            inputSimulatorPool: pool);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = originReset.Received(1).TryResetToDesktopOrigin();
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = await pool.Received(1).AcquireAsync(3840, 1080, Arg.Any<CancellationToken>());
        pool.Received(1).Release(simulator);
    }

    [Fact]
    public async Task StartRecordingAsync_WithSkippedCornerReset_DoesNotLeasePooledSimulator()
    {
        var pool = Substitute.For<IInputSimulatorPool>();
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            positionProvider: null,
            inputSimulatorPool: pool);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: true,
            cancellationToken: TestContext.Current.CancellationToken);

        _ = pool.DidNotReceive().AcquireAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRecordingAsync_WithGlobalAbsoluteSimulator_PreservesDesktopOrigin()
    {
        var mockSimulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)mockSimulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(Task.FromResult<ScreenRect?>(new ScreenRect(-1920, -200, 4480, 1640)));
        using var recorder = new MacroRecorder(
            _captureFactory,
            _strategyFactory,
            _processorFactory,
            () => mockSimulator,
            positionProvider);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: false,
            cancellationToken: TestContext.Current.CancellationToken);

        mockSimulator.Received(1).MoveAbsolute(-1920, -200);
        mockSimulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureCompletesAfterStop_DoesNotThrow()
    {
        var captureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captureRunTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _capture.ProviderName.Returns("TestCapture");
        _ = _capture.StartAsync(Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            _ = captureStarted.TrySetResult();
            return captureRunTcs.Task;
        });

        using var recorder = CreateRecorder();

        var startTask = recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await captureStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, TestContext.Current.CancellationToken);
        var stopResult = recorder.StopRecording();
        captureRunTcs.SetResult();

        _ = stopResult.Should().NotBeNull();
        Func<Task> act = async () => await startTask;
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStoppedDuringStrategyInitialization_DoesNotAttachCaptureAfterStop()
    {
        var initializationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _strategy.InitializeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            initializationEntered.TrySetResult();
            return releaseInitialization.Task;
        });
        using var recorder = CreateRecorder();

        var startTask = recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await initializationEntered.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = recorder.StopRecording();
        releaseInitialization.TrySetResult();

        await startTask;

        _capture.DidNotReceive().Configure(Arg.Any<bool>(), Arg.Any<bool>());
        await _capture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyFallsBackToRelative_UsesRelativeModeForProcessorAndSequence()
    {
        var relativeStrategy = new RelativeCoordinateStrategy();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        using var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false, cancellationToken: TestContext.Current.CancellationToken);
        var sequence = recorder.StopRecording();

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
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        using var recorder = new MacroRecorder(
            () => modeAwareCapture,
            _strategyFactory,
            _processorFactory);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: false,
            skipInitialZero: true,
            cancellationToken: TestContext.Current.CancellationToken);
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
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        using var recorder = new MacroRecorder(
            () => modeAwareCapture,
            _strategyFactory,
            _processorFactory);

        await recorder.StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            forceRelative: true,
            skipInitialZero: true,
            cancellationToken: TestContext.Current.CancellationToken);
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
        var relativeStrategy = Substitute.For<IRelativeCoordinateStrategy>();
        _ = relativeStrategy.ProducesRelativeCoordinates.Returns(returnThis: true);
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        using var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false, cancellationToken: TestContext.Current.CancellationToken);
        var sequence = recorder.StopRecording();

        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: false);
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        await mockSimulator.Received(1).InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        mockSimulator.Received(2).MoveRelative(-20000, 0);
        mockSimulator.Received(2).MoveRelative(0, -20000);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenStrategyFallsBackToRelative_AndSkipInitialZero_LeavesCursorAsIs()
    {
        var relativeStrategy = new RelativeCoordinateStrategy();
        _ = _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(relativeStrategy);
        var mockSimulator = Substitute.For<IInputSimulator>();
        using var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, forceRelative: false, skipInitialZero: true, cancellationToken: TestContext.Current.CancellationToken);
        var sequence = recorder.StopRecording();

        _processor.Received(1).Configure(recordMouse: true, recordKeyboard: true, Arg.Is<HashSet<int>>(x => x == null), isAbsoluteCoordinates: false);
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        mockSimulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenInputCaptureFactoryMissing_ThrowsAndResetsState()
    {
        using var recorder = new MacroRecorder(inputCaptureFactory: null, _strategyFactory, _processorFactory);

        var act = async () => await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No input capture factory configured*");
        _ = recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureStartThrows_CleansUpAndRethrows()
    {
        _ = _capture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("start failed")));
        using var recorder = CreateRecorder();

        var act = async () => await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("start failed");
        _ = recorder.IsRecording.Should().BeFalse();
        _capture.Received(1).StopCapture();
        _capture.Received(1).Dispose();
    }

    [Fact]
    public async Task StopRecording_WhenCaptureStopThrows_ReturnsRecordedMacro()
    {
        using var recorder = CreateRecorder();
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);
        _capture.When(x => x.StopCapture()).Do(_ => throw new InvalidOperationException("stop fail"));

        var result = recorder.StopRecording();

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
        using var recorder = CreateRecorder();
        await recorder.StartRecordingAsync(recordMouse: true, recordKeyboard: true, cancellationToken: TestContext.Current.CancellationToken);

        var inputTask = Task.Run(() =>
            _capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
                this,
                new CapturedInputEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 }),
            TestContext.Current.CancellationToken);

        await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
        MacroSequence? sequence = null;
        var stopThread = new Thread(() => sequence = recorder.StopRecording());
        stopThread.Start();

        try
        {
            var blockedOrFinished = SpinWait.SpinUntil(
                () => stopThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin) || !stopThread.IsAlive,
                TimeSpan.FromSeconds(5));
            Assert.True(blockedOrFinished);
            Assert.True(stopThread.IsAlive, "StopRecording returned while an input callback was still processing.");
        }
        finally
        {
            _ = releaseProcessing.TrySetResult();
        }

        Assert.True(stopThread.Join(TimeSpan.FromSeconds(5)));
        await inputTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.NotNull(sequence);

        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
    }
}
