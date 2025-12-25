namespace CrossMacro.Core.Tests.Models;

using CrossMacro.Core.Models;
using FluentAssertions;

public class MacroSequenceTests
{
    [Fact]
    public void IsValid_EmptyEvents_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence();

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_NullEvents_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence { Events = null! };

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_EventWithNegativeTimestamp_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, Timestamp = -1, DelayMs = 0 }
            }
        };

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_EventWithNegativeDelay_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = -100 }
            }
        };

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ValidEvents_ReturnsTrue()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = 0, X = 100, Y = 200 },
                new() { Type = EventType.ButtonPress, Timestamp = 100, DelayMs = 100, Button = MouseButton.Left },
                new() { Type = EventType.ButtonRelease, Timestamp = 150, DelayMs = 50, Button = MouseButton.Left }
            }
        };

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CalculateDuration_EmptyEvents_SetsDurationToZero()
    {
        // Arrange
        var macro = new MacroSequence();

        // Act
        macro.CalculateDuration();

        // Assert
        macro.TotalDurationMs.Should().Be(0);
    }

    [Fact]
    public void CalculateDuration_WithEvents_SetsDurationToLastTimestamp()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Timestamp = 0 },
                new() { Timestamp = 500 },
                new() { Timestamp = 1500 }
            }
        };

        // Act
        macro.CalculateDuration();

        // Assert
        macro.TotalDurationMs.Should().Be(1500);
    }

    [Fact]
    public void EventCount_ReturnsCorrectCount()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new(), new(), new(), new(), new()
            }
        };

        // Act & Assert
        macro.EventCount.Should().Be(5);
    }

    [Fact]
    public void NewMacroSequence_HasDefaultValues()
    {
        // Arrange & Act
        var macro = new MacroSequence();

        // Assert
        macro.Id.Should().NotBeEmpty();
        macro.Name.Should().Be("Unnamed Macro");
        macro.Events.Should().NotBeNull();
        macro.Events.Should().BeEmpty();
        macro.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
