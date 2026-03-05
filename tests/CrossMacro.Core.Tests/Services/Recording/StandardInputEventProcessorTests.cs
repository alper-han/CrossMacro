using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Recording;

public class StandardInputEventProcessorTests
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
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((10, 20));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.MouseMove);
        result.Value.X.Should().Be(10);
        result.Value.Y.Should().Be(20);
    }

    [Fact]
    public void Process_MouseMove_ShouldReturnNull_WhenNotRecordingMouse()
    {
        // Arrange
        _processor.Configure(recordMouse: false, recordKeyboard: true, ignoredKeys: null);
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((10, 20));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Process_MouseMove_ShouldReturnNull_WhenZeroDelta()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((0, 0));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Key Event Tests

    [Fact]
    public void Process_KeyEvent_ShouldReturnEvent_WhenRecordingKeyboard()
    {
        // Arrange
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 }; // KEY_A press

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.KeyPress);
        result.Value.KeyCode.Should().Be(30);
    }

    [Fact]
    public void Process_KeyEvent_ShouldReturnNull_WhenNotRecordingKeyboard()
    {
        // Arrange
        _processor.Configure(recordMouse: true, recordKeyboard: false, ignoredKeys: null);
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Process_KeyEvent_ShouldReturnNull_WhenKeyIsIgnored()
    {
        // Arrange
        var ignoredKeys = new HashSet<int> { 30 }; // Ignore KEY_A
        _processor.Configure(recordMouse: true, recordKeyboard: true, ignoredKeys: ignoredKeys);
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Process_KeyRelease_ShouldReturnKeyReleaseEvent()
    {
        // Arrange
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 0 }; // KEY_A release

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.KeyRelease);
    }

    [Fact]
    public void Process_KeyRepeat_ShouldReturnNull()
    {
        // Arrange - value 2 = repeat
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 2 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert - repeats are filtered
        result.Should().BeNull();
    }

    #endregion

    #region Mouse Button Tests

    [Fact]
    public void Process_MouseButton_ShouldReturnButtonPressEvent()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((100, 100));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseButton, Code = InputEventCode.BTN_LEFT, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.ButtonPress);
        result.Value.Button.Should().Be(MouseButton.Left);
    }

    [Fact]
    public void Process_MouseButton_ShouldReturnButtonReleaseEvent()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((100, 100));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseButton, Code = InputEventCode.BTN_LEFT, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.ButtonRelease);
    }

    #endregion

    #region Scroll Tests

    [Fact]
    public void Process_MouseScroll_ShouldReturnScrollUpEvent()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((100, 100));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseScroll, Code = 0, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.Click);
        result.Value.Button.Should().Be(MouseButton.ScrollUp);
    }

    [Fact]
    public void Process_MouseScroll_ShouldReturnScrollDownEvent()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((100, 100));
        var args = new InputCaptureEventArgs { Type = InputEventType.MouseScroll, Code = 0, Value = -1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Button.Should().Be(MouseButton.ScrollDown);
    }

    #endregion

    #region Sync Tests

    [Fact]
    public void Process_Sync_ShouldFlushBufferedDeltas()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((15, 25));
        var args = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().NotBeNull();
        result.Value.Type.Should().Be(EventType.MouseMove);
        result.Value.X.Should().Be(15);
        result.Value.Y.Should().Be(25);
    }

    [Fact]
    public void Process_Sync_ShouldReturnNull_WhenZeroDeltas()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((0, 0));
        var args = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    [Fact]
    public void Process_UnknownInputType_ShouldReturnNull()
    {
        // Arrange
        _strategy.ProcessPosition(Arg.Any<InputCaptureEventArgs>()).Returns((50, 60));
        var args = new InputCaptureEventArgs { Type = InputEventType.Unknown, Code = 999, Value = 1 };

        // Act
        var result = _processor.Process(args, timestamp: 1000);

        // Assert
        result.Should().BeNull();
    }
}
