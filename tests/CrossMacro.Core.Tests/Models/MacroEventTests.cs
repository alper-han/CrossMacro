namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class MacroEventTests
{
    [Fact]
    public void NewMacroEvent_HasDefaultValues()
    {
        // Arrange & Act
        var ev = new MacroEvent();

        // Assert
        ev.Type.Should().Be(EventType.None); // Default enum value
        ev.X.Should().Be(0);
        ev.Y.Should().Be(0);
        ev.Button.Should().Be(MouseButton.None);
        ev.Timestamp.Should().Be(0);
        ev.DelayMs.Should().Be(0);
        ev.HasRandomDelay.Should().BeFalse();
        ev.RandomDelayMinMs.Should().Be(0);
        ev.RandomDelayMaxMs.Should().Be(0);
        ev.KeyCode.Should().Be(0);
    }

    [Theory]
    [InlineData(EventType.ButtonPress)]
    [InlineData(EventType.ButtonRelease)]
    [InlineData(EventType.MouseMove)]
    [InlineData(EventType.Click)]
    [InlineData(EventType.KeyPress)]
    [InlineData(EventType.KeyRelease)]
    public void MacroEvent_CanSetAllEventTypes(EventType eventType)
    {
        // Arrange & Act
        var ev = new MacroEvent { Type = eventType };

        // Assert
        ev.Type.Should().Be(eventType);
    }

    [Theory]
    [InlineData(MouseButton.None)]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    [InlineData(MouseButton.ScrollUp)]
    [InlineData(MouseButton.ScrollDown)]
    public void MacroEvent_CanSetAllMouseButtons(MouseButton button)
    {
        // Arrange & Act
        var ev = new MacroEvent { Button = button };

        // Assert
        ev.Button.Should().Be(button);
    }

    [Fact]
    public void MacroEvent_CanSetCoordinates()
    {
        // Arrange & Act
        var ev = new MacroEvent { X = 1920, Y = 1080 };

        // Assert
        ev.X.Should().Be(1920);
        ev.Y.Should().Be(1080);
    }

    [Fact]
    public void MacroEvent_CanSetNegativeCoordinates()
    {
        // Some scenarios might have negative relative coordinates
        var ev = new MacroEvent { X = -100, Y = -50 };

        ev.X.Should().Be(-100);
        ev.Y.Should().Be(-50);
    }

    [Fact]
    public void MacroEvent_CanSetKeyCode()
    {
        // Arrange - Linux KEY_A = 30
        var ev = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 };

        // Assert
        ev.KeyCode.Should().Be(30);
    }
}
