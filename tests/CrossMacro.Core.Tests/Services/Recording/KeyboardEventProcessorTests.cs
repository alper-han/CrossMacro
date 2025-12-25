namespace CrossMacro.Core.Tests.Services.Recording;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording;
using FluentAssertions;

public class KeyboardEventProcessorTests
{
    private readonly KeyboardEventProcessor _processor;

    public KeyboardEventProcessorTests()
    {
        _processor = new KeyboardEventProcessor();
    }

    [Fact]
    public void CanProcess_KeyEvent_ReturnsTrue()
    {
        // Act
        var result = _processor.CanProcess(InputEventCode.EV_KEY);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanProcess_RelEvent_ReturnsFalse()
    {
        // Act
        var result = _processor.CanProcess(InputEventCode.EV_REL);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ProcessEvent_WrongEventType_ReturnsNull()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, 30, 1, 100, 0, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_KeyPress_ReturnsKeyPressEvent()
    {
        // Arrange - KEY_A = 30, value 1 = press
        ushort keyA = 30;

        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, keyA, 1, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.KeyPress);
        result.KeyCode.Should().Be(30);
    }

    [Fact]
    public void ProcessEvent_KeyRelease_ReturnsKeyReleaseEvent()
    {
        // Arrange - KEY_A = 30, value 0 = release
        ushort keyA = 30;

        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, keyA, 0, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.KeyRelease);
        result.KeyCode.Should().Be(30);
    }

    [Fact]
    public void ProcessEvent_MouseButton_ReturnsNull()
    {
        // Mouse buttons should be handled by MouseButtonEventProcessor
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.BTN_LEFT, 1, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_KeyCodeOutOfRange_ReturnsNull()
    {
        // Key codes should be 1-255
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, 0, 1, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_KeyRepeat_ReturnsNull()
    {
        // value 2 = key repeat, should be ignored
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, 30, 2, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_SetsTimestamp()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, 30, 1, 12345, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(12345);
    }

    [Fact]
    public void Reset_DoesNotThrow()
    {
        // Act
        var act = () => _processor.Reset();

        // Assert
        act.Should().NotThrow();
    }
}
