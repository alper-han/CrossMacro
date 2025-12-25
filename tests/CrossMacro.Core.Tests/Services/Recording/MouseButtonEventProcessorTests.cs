namespace CrossMacro.Core.Tests.Services.Recording;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording;
using FluentAssertions;

public class MouseButtonEventProcessorTests
{
    private readonly MouseButtonEventProcessor _processor;

    public MouseButtonEventProcessorTests()
    {
        _processor = new MouseButtonEventProcessor();
    }

    [Fact]
    public void CanProcess_KeyEvent_ReturnsTrue()
    {
        // Mouse buttons are reported as EV_KEY
        var result = _processor.CanProcess(InputEventCode.EV_KEY);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanProcess_RelEvent_ReturnsFalse()
    {
        var result = _processor.CanProcess(InputEventCode.EV_REL);

        result.Should().BeFalse();
    }

    [Fact]
    public void ProcessEvent_LeftButtonPress_ReturnsButtonPressEvent()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_LEFT, 1, 100, 500, 600);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.ButtonPress);
        result.Button.Should().Be(MouseButton.Left);
        result.X.Should().Be(500);
        result.Y.Should().Be(600);
    }

    [Fact]
    public void ProcessEvent_LeftButtonRelease_ReturnsButtonReleaseEvent()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_LEFT, 0, 100, 500, 600);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.ButtonRelease);
        result.Button.Should().Be(MouseButton.Left);
    }

    [Fact]
    public void ProcessEvent_RightButton_ReturnsRightButton()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_RIGHT, 1, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Button.Should().Be(MouseButton.Right);
    }

    [Fact]
    public void ProcessEvent_MiddleButton_ReturnsMiddleButton()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_MIDDLE, 1, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Button.Should().Be(MouseButton.Middle);
    }

    [Fact]
    public void ProcessEvent_KeyboardKey_ReturnsNull()
    {
        // Regular keyboard keys should be ignored
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, 30, 1, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_WrongEventType_ReturnsNull()
    {
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.BTN_LEFT, 1, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_SetsCoordinates()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_LEFT, 1, 100, 1920, 1080);

        // Assert
        result.Should().NotBeNull();
        result!.X.Should().Be(1920);
        result.Y.Should().Be(1080);
    }

    [Fact]
    public void ProcessEvent_SetsTimestamp()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_LEFT, 1, 99999, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(99999);
    }

    [Fact]
    public void Reset_DoesNotThrow()
    {
        var act = () => _processor.Reset();

        act.Should().NotThrow();
    }
}
