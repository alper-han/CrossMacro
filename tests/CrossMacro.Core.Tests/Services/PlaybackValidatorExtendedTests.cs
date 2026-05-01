using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System;

namespace CrossMacro.Core.Tests.Services;

public class PlaybackValidatorExtendedTests
{
    private readonly PlaybackValidator _validator = new();

    [Fact]
    public void Validate_WithInvalidEventType_ReturnsError()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = (EventType)999 } // Invalid enum val
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invalid/undefined EventType"));
    }

    [Fact]
    public void Validate_WithNoneEventType_ReturnsWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.None }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        // Should be valid (warn only)
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("None"));
    }
}
