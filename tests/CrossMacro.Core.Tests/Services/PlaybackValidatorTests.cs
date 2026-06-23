namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

public class PlaybackValidatorTests
{
    private readonly PlaybackValidator _validator;

    public PlaybackValidatorTests()
    {
        _validator = new PlaybackValidator(CreateKeyCodeMapper());
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
    public void Validate_WhenHiddenScreenReadScriptContainsUnsupportedStep_ReturnsError()
    {
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelcolor 1 2 sampled", "bogus"]
        };

        var result = _validator.Validate(macro);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Macro script steps are invalid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tap Backspace")]
    [InlineData("tap F13")]
    [InlineData("tap NumpadPlus")]
    public void Validate_WhenScriptUsesRuntimeMappedKey_ReturnsNoErrors(string scriptStep)
    {
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelcolor 1 2 sampled", scriptStep]
        };

        var result = _validator.Validate(macro);

        result.Errors.Should().BeEmpty();
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
    public void Validate_AbsoluteMacroWithZeroZeroButtonAndNonZeroMouseContext_ReturnsWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 500, Y = 300 },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 0, Y = 0 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("(0,0)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ExplicitAbsoluteButtonEventInRelativeMacro_ReturnsWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 500, Y = 300, CoordinateMode = MouseCoordinateMode.Relative },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 0, Y = 0, CoordinateMode = MouseCoordinateMode.Absolute }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("(0,0)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ExplicitRelativeButtonEventInAbsoluteMacro_DoesNotReturnZeroZeroWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 500, Y = 300, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 0, Y = 0, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().NotContain(w => w.Contains("(0,0)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_LegacyAbsoluteButtonEventWithNullMode_ReturnsWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 400, Y = 250 },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 0, Y = 0 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("(0,0)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CurrentPositionButtonEventWithNullMode_DoesNotReturnZeroZeroWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 400, Y = 250 },
                new() { Type = EventType.Click, Button = MouseButton.Left, UseCurrentPosition = true, X = 0, Y = 0 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().NotContain(w => w.Contains("(0,0)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ScrollEventWithNullMode_DoesNotReturnZeroZeroWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 400, Y = 250 },
                new() { Type = EventType.Click, Button = MouseButton.ScrollUp, X = 0, Y = 0 }
            }
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        result.Warnings.Should().NotContain(w => w.Contains("(0,0)", StringComparison.Ordinal));
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

        var validator = new PlaybackValidator(CreateKeyCodeMapper(), positionProvider);
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
        var validator = new PlaybackValidator(CreateKeyCodeMapper());
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

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        return new KeyCodeMapper(new TestKeyboardLayoutService());
    }

    private sealed class TestKeyboardLayoutService : IKeyboardLayoutService
    {
        public string GetKeyName(int keyCode)
        {
            return keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public int GetKeyCode(string keyName)
        {
            return -1;
        }

        public char? GetCharFromKeyCode(
            int keyCode,
            bool leftShift,
            bool rightShift,
            bool rightAlt,
            bool leftAlt,
            bool leftCtrl,
            bool capsLock)
        {
            return null;
        }

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
        {
            return null;
        }
    }
}
