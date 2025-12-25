namespace CrossMacro.Core.Tests.Services.Recording;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording;
using FluentAssertions;

public class MouseScrollEventProcessorTests
{
    private readonly MouseScrollEventProcessor _processor;

    public MouseScrollEventProcessorTests()
    {
        _processor = new MouseScrollEventProcessor();
    }

    [Fact]
    public void CanProcess_RelEvent_ReturnsTrue()
    {
        var result = _processor.CanProcess(InputEventCode.EV_REL);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanProcess_KeyEvent_ReturnsFalse()
    {
        var result = _processor.CanProcess(InputEventCode.EV_KEY);

        result.Should().BeFalse();
    }

    [Fact]
    public void ProcessEvent_ScrollUp_ReturnsScrollUpEvent()
    {
        // Act - positive value = scroll up
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.REL_WHEEL, 1, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.Click);
        result.Button.Should().Be(MouseButton.ScrollUp);
    }

    [Fact]
    public void ProcessEvent_ScrollDown_ReturnsScrollDownEvent()
    {
        // Act - negative value = scroll down
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.REL_WHEEL, -1, 100, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(EventType.Click);
        result.Button.Should().Be(MouseButton.ScrollDown);
    }

    [Fact]
    public void ProcessEvent_MouseMove_ReturnsNull()
    {
        // REL_X and REL_Y should be ignored by this processor
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.REL_X, 10, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_WrongEventType_ReturnsNull()
    {
        var result = _processor.ProcessEvent(InputEventCode.EV_KEY, InputEventCode.REL_WHEEL, 1, 100, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public void ProcessEvent_SetsTimestamp()
    {
        // Act
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.REL_WHEEL, 1, 54321, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(54321);
    }

    [Fact]
    public void ProcessEvent_LargeScrollValue_StillWorks()
    {
        // Some mice report larger scroll values
        var result = _processor.ProcessEvent(InputEventCode.EV_REL, InputEventCode.REL_WHEEL, 5, 100, 0, 0);

        result.Should().NotBeNull();
        result!.Button.Should().Be(MouseButton.ScrollUp);
    }

    [Fact]
    public void Reset_DoesNotThrow()
    {
        var act = () => _processor.Reset();

        act.Should().NotThrow();
    }
}
