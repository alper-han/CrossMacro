namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using FluentAssertions;
using NSubstitute;
using System.Threading;

/// <summary>
/// Tests for MacroRecorder focusing on initialization and error handling
/// </summary>
public class MacroRecorderTests
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
        _strategyFactory.Create(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(_strategy);
        
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
        recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_NoMouseNoKeyboard_ThrowsArgumentException()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        var act = async () => await recorder.StartRecordingAsync(
            recordMouse: false, 
            recordKeyboard: false);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
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
        act.Should().Throw<InvalidOperationException>()
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
        result.Should().BeNull();
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
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StartRecordingAsync_CapturesEvents_WhenInputReceived()
    {
        // Arrange


        var recorder = CreateRecorder();
        
        var receivedEvents = new List<MacroEvent>();
        recorder.EventRecorded += (s, e) => receivedEvents.Add(e);

        // Setup processor to return an event when Process is called
        _processor.Process(Arg.Any<InputCaptureEventArgs>(), Arg.Any<long>())
            .Returns(new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 });

        // Act
        await recorder.StartRecordingAsync(true, true);
        
        // Simulate input
        _capture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(
            this, 
            new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 });

        recorder.StopRecording();

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].Type.Should().Be(EventType.KeyPress);
        receivedEvents[0].KeyCode.Should().Be(30);
    }

    [Fact]
    public async Task StartRecordingAsync_InitializesStrategyAndProcessor()
    {
        // Arrange
        var recorder = CreateRecorder();

        // Act
        await recorder.StartRecordingAsync(true, true);
        
        // Assert
        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        _processor.Received(1).Configure(true, true, Arg.Is<HashSet<int>>(x => x == null), true);
    }
    [Fact]
    public async Task StartRecordingAsync_WithForceRelative_PerformsCornerReset()
    {
        // Arrange
        var mockSimulator = Substitute.For<IInputSimulator>();
        var recorder = new MacroRecorder(_captureFactory, _strategyFactory, _processorFactory, () => mockSimulator);

        // Act
        await recorder.StartRecordingAsync(true, true, forceRelative: true, skipInitialZero: false);
        
        // Assert
        // Verify Corner Reset logic: Initialize() then MoveRelative(-20000, -20000)
        mockSimulator.Received(1).Initialize();
        mockSimulator.Received(1).MoveRelative(-20000, -20000);
        
        await _strategy.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureCompletesAfterStop_DoesNotThrow()
    {
        // Arrange
        var captureRunTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _capture.ProviderName.Returns("TestCapture");
        _capture.StartAsync(Arg.Any<CancellationToken>()).Returns(captureRunTcs.Task);

        var recorder = CreateRecorder();

        // Act
        var startTask = recorder.StartRecordingAsync(true, true);
        await Task.Yield();
        var stopResult = recorder.StopRecording();
        captureRunTcs.SetResult();

        // Assert
        stopResult.Should().NotBeNull();
        Func<Task> act = async () => await startTask;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenInputCaptureFactoryMissing_ThrowsAndResetsState()
    {
        // Arrange
        var recorder = new MacroRecorder(null, _strategyFactory, _processorFactory);

        // Act
        var act = async () => await recorder.StartRecordingAsync(true, true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No input capture factory configured*");
        recorder.IsRecording.Should().BeFalse();
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCaptureStartThrows_CleansUpAndRethrows()
    {
        // Arrange
        _capture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("start failed")));
        var recorder = CreateRecorder();

        // Act
        var act = async () => await recorder.StartRecordingAsync(true, true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("start failed");
        recorder.IsRecording.Should().BeFalse();
        _capture.Received(1).Stop();
        _capture.Received(1).Dispose();
    }

    [Fact]
    public async Task StopRecording_WhenCaptureStopThrows_ReturnsRecordedMacro()
    {
        // Arrange
        var recorder = CreateRecorder();
        await recorder.StartRecordingAsync(true, true);
        _capture.When(x => x.Stop()).Do(_ => throw new Exception("stop fail"));

        // Act
        var result = recorder.StopRecording();

        // Assert
        result.Should().NotBeNull();
        recorder.IsRecording.Should().BeFalse();
    }
}
