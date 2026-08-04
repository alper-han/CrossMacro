
namespace CrossMacro.Core.Tests.Services.Recording;

public sealed class StandardInputEventProcessorTests
{
    private readonly ICoordinateStrategy _strategy;
    private readonly StandardInputEventProcessor _processor;

    public StandardInputEventProcessorTests()
    {
        _strategy = Substitute.For<ICoordinateStrategy>();
        _processor = new StandardInputEventProcessor(_strategy);

        // Default configuration: record both mouse and keyboard
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: null);
    }

    #region Mouse Move Tests

    [Fact]
    public void Process_MouseMove_ShouldReturnEvent_WhenRecordingMouse()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(10, 20));
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.MouseMove);
        _ = result.Value.X.Should().Be(10);
        _ = result.Value.Y.Should().Be(20);
    }

    [Theory]
    [InlineData(true, MouseCoordinateMode.Absolute)]
    [InlineData(false, MouseCoordinateMode.Relative)]
    public void Process_MouseMove_ShouldStampCoordinateModeFromRecordingSession(bool isAbsoluteCoordinates, MouseCoordinateMode expectedMode)
    {
        // Arrange
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: null, isAbsoluteCoordinates: isAbsoluteCoordinates);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(10, 20));
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.CoordinateMode.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData(true, MouseCoordinateSpace.LogicalDesktop)]
    [InlineData(false, MouseCoordinateSpace.RawDevice)]
    public void Process_MouseMove_ShouldStampCoordinateSpaceFromStrategy(
        bool producesLogicalCoordinates,
        MouseCoordinateSpace expectedSpace)
    {
        _ = _strategy.ProducesLogicalCoordinates.Returns(producesLogicalCoordinates);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(10, 20));
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        var result = _processor.Process(args, timestamp: 1000);

        _ = result.Should().NotBeNull();
        _ = result.Value.CoordinateSpace.Should().Be(expectedSpace);
    }

    [Fact]
    public void Process_MouseMove_WhenAbsoluteSampleIsOrigin_EmitsOrigin()
    {
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: null, isAbsoluteCoordinates: true);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(0, 0));
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 0 };

        var result = _processor.Process(args, timestamp: 1000);

        _ = result.Should().NotBeNull();
        _ = result.Value.X.Should().Be(0);
        _ = result.Value.Y.Should().Be(0);
    }

    [Fact]
    public void Process_MouseMove_ShouldReturnNull_WhenNotRecordingMouse()
    {
        // Arrange
        _processor.Configure(recordMouse: false, recordKeyboard: true, ignoredKeys: null);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(10, 20));
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    public void Process_MouseMove_ShouldReturnNull_WhenZeroDelta()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.None);
        var args = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }

    #endregion

    #region Key Event Tests

    [Fact]
    public void Process_KeyEvent_ShouldReturnEvent_WhenRecordingKeyboard()
    {
        // Arrange
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 }; // KEY_A press

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.KeyPress);
        _ = result.Value.KeyCode.Should().Be(30);
    }

    [Fact]
    public void Process_KeyEvent_ShouldReturnNull_WhenNotRecordingKeyboard()
    {
        // Arrange
        _processor.Configure(recordMouse: true, recordKeyboard: false, ignoredKeys: null);
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    public void Process_KeyEvent_ShouldReturnNull_WhenKeyIsIgnored()
    {
        // Arrange
        var ignoredKeys = new HashSet<int> { 30 }; // Ignore KEY_A
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: ignoredKeys);
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    public void Process_KeyRelease_ShouldReturnKeyReleaseEvent()
    {
        // Arrange
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 0 }; // KEY_A release

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.KeyRelease);
    }

    [Fact]
    public void Process_KeyRepeat_ShouldReturnNull()
    {
        // Arrange - value 2 = repeat
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 2 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert - repeats are filtered
        _ = result.Should().BeNull();
    }

    #endregion

    #region Mouse Button Tests

    [Fact]
    public void Process_MouseButton_ShouldReturnButtonPressEvent()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseButton, Code = InputEventCode.BTN_LEFT, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.ButtonPress);
        _ = result.Value.Button.Should().Be(MacroMouseButton.Left);
    }

    [Theory]
    [InlineData(true, MouseCoordinateMode.Absolute)]
    [InlineData(false, MouseCoordinateMode.Relative)]
    public void Process_MouseButton_ShouldStampCoordinateModeFromRecordingSession(bool isAbsoluteCoordinates, MouseCoordinateMode expectedMode)
    {
        // Arrange
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: null, isAbsoluteCoordinates: isAbsoluteCoordinates);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseButton, Code = InputEventCode.BTN_LEFT, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.CoordinateMode.Should().Be(expectedMode);
    }

    [Fact]
    public void Process_MouseButton_ShouldReturnButtonReleaseEvent()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseButton, Code = InputEventCode.BTN_LEFT, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.ButtonRelease);
    }

    #endregion

    #region Scroll Tests

    [Fact]
    public void Process_MouseScroll_ShouldReturnScrollUpEvent()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseScroll, Code = 0, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.Click);
        _ = result.Value.Button.Should().Be(MacroMouseButton.ScrollUp);
        _ = result.Value.CoordinateMode.Should().BeNull();
    }

    [Fact]
    public void Process_MouseScroll_ShouldReturnScrollDownEvent()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseScroll, Code = 0, Value = -1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Button.Should().Be(MacroMouseButton.ScrollDown);
        _ = result.Value.CoordinateMode.Should().BeNull();
    }

    [Fact]
    public void Process_MouseScroll_ShouldReturnHorizontalScrollEvents()
    {
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(100, 100));
        var args = new CapturedInputEvent { Type = InputEventType.MouseScroll, Code = InputEventCode.REL_HWHEEL, Value = 1 };

        var result = _processor.Process(args, timestamp: 1000);

        _ = result.Should().NotBeNull();
        _ = result.Value.Button.Should().Be(MacroMouseButton.ScrollRight);
    }

    #endregion

    #region Sync Tests

    [Fact]
    public void Process_Sync_ShouldFlushBufferedDeltas()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(15, 25));
        var args = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.Type.Should().Be(EventType.MouseMove);
        _ = result.Value.X.Should().Be(15);
        _ = result.Value.Y.Should().Be(25);
    }

    [Theory]
    [InlineData(true, MouseCoordinateMode.Absolute)]
    [InlineData(false, MouseCoordinateMode.Relative)]
    public void Process_Sync_ShouldStampCoordinateModeFromRecordingSession(bool isAbsoluteCoordinates, MouseCoordinateMode expectedMode)
    {
        // Arrange
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: null, isAbsoluteCoordinates: isAbsoluteCoordinates);
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(15, 25));
        var args = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Value.CoordinateMode.Should().Be(expectedMode);
    }

    [Fact]
    public void Process_Sync_ShouldReturnNull_WhenZeroDeltas()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.None);
        var args = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }

    #endregion

    [Fact]
    public void Process_UnknownInputType_ShouldReturnNull()
    {
        // Arrange
        _ = _strategy.ProcessPosition(Arg.Any<CapturedInputEvent>()).Returns(CoordinateSample.Create(50, 60));
        var args = new CapturedInputEvent { Type = InputEventType.Unknown, Code = 999, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        _ = result.Should().BeNull();
    }
}
