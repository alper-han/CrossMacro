
namespace CrossMacro.Core.Tests.Services;

public sealed class PlaybackValidatorExtendedTests
{
    private readonly PlaybackValidator _validator = new(CreateKeyCodeMapper());

    [Fact]
    public void Validate_WithInvalidEventType_ReturnsError()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = (EventType)999 }, // Invalid enum val
            },
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(e => e.Contains("invalid/undefined EventType"));
    }

    [Fact]
    public void Validate_WithNoneEventType_ReturnsWarning()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.None },
            },
        };

        // Act
        var result = _validator.Validate(macro);

        // Assert
        // Should be valid (warn only)
        _ = result.IsValid.Should().BeTrue();
        _ = result.Warnings.Should().Contain(w => w.Contains("None"));
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
