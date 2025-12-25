namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using FluentAssertions;
using NSubstitute;

public class PlaybackValidatorTests
{
    private readonly PlaybackValidator _validator;

    public PlaybackValidatorTests()
    {
        _validator = new PlaybackValidator();
    }

    [Fact]
    public void Validate_NullMacro_ReturnsError()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Macro is empty or null");
    }

    [Fact]
    public void Validate_EmptyEvents_ReturnsError()
    {
        // Arrange
        var macro = new MacroSequence();

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Macro is empty or null");
    }

    [Fact]
    public void Validate_ValidMacro_ReturnsNoErrors()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 200 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_LongDelay_ReturnsWarning()
    {
        // Arrange - Delay over 10 seconds
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, DelayMs = 15000 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("delay") || w.Contains("10 seconds"));
    }

    [Fact]
    public void Validate_VeryLongMacro_ReturnsWarning()
    {
        // Arrange - Macro over 5 minutes (300000ms)
        var macro = new MacroSequence
        {
            TotalDurationMs = 350000, // ~5.8 minutes
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("long") || w.Contains("minutes"));
    }

    [Fact]
    public void Validate_ManyEvents_ReturnsWarning()
    {
        // Arrange - Over 10000 events
        var events = Enumerable.Range(0, 10001)
            .Select(_ => new MacroEvent { Type = EventType.MouseMove })
            .ToList();

        var macro = new MacroSequence { Events = events };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("10000") || w.Contains("events"));
    }

    [Fact]
    public void Validate_WithUnsupportedPositionProvider_ReturnsWarning()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.IsSupported.Returns(false);
        positionProvider.ProviderName.Returns("MockProvider");

        var validator = new PlaybackValidator(positionProvider);
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove }
            }
        };

        // Act
        var result = validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => 
            w.Contains("not supported") || w.Contains("MockProvider"));
    }

    [Fact]
    public void Validate_WithNullPositionProvider_ReturnsWarning()
    {
        // Arrange
        var validator = new PlaybackValidator(null);
        var macro = new MacroSequence
        {
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove }
            }
        };

        // Act
        var result = validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("fallback") || w.Contains("provider"));
    }

    [Fact]
    public void ValidationResult_IsValid_TrueWhenNoErrors()
    {
        // Arrange
        var result = new ValidationResult();
        result.AddWarning("Some warning");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void ValidationResult_IsValid_FalseWhenHasErrors()
    {
        // Arrange
        var result = new ValidationResult();
        result.AddError("Some error");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
    }
}
