namespace CrossMacro.Core.Tests.Models;

using System;
using System.Collections.Generic;
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
    public void IsValid_EventWithInvalidRandomDelayBounds_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.MouseMove,
                    Timestamp = 0,
                    DelayMs = 0,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 200,
                    RandomDelayMaxMs = 100
                }
            }
        };

        // Act
        var result = macro.IsValid();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_TrailingRandomDelayWithInvalidBounds_ReturnsFalse()
    {
        // Arrange
        var macro = new MacroSequence
        {
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 300,
            TrailingDelayMaxMs = 100,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = 0 }
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
    public void Clone_CreatesDetachedCopy()
    {
        // Arrange
        var original = new MacroSequence
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 10, Y = 20, Timestamp = 30, DelayMs = 40 }
            },
            ScriptSteps = new List<string> { "move 10 20" },
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TotalDurationMs = 30,
            RecordedAt = new DateTime(2026, 1, 1, 0, 0, 10, DateTimeKind.Utc),
            ActualDuration = TimeSpan.FromSeconds(2),
            MouseMoveCount = 1,
            ClickCount = 0,
            EventsPerSecond = 0.5,
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            TrailingDelayMs = 100,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 50,
            TrailingDelayMaxMs = 150
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.Should().NotBeSameAs(original);
        clone.Should().BeEquivalentTo(original);
        clone.Events.Should().NotBeSameAs(original.Events);
        clone.ScriptSteps.Should().NotBeSameAs(original.ScriptSteps);

        clone.Name = "Updated";
        clone.Events.Add(new MacroEvent { Type = EventType.KeyPress });
        clone.ScriptSteps.Add("press a");

        original.Name.Should().Be("Original");
        original.Events.Should().HaveCount(1);
        original.ScriptSteps.Should().ContainSingle();
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
