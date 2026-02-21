using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;

namespace CrossMacro.Infrastructure.Tests.Services;

public class EditorActionValidatorTests
{
    private readonly EditorActionValidator _validator = new();

    [Fact]
    public void Validate_MouseButtonWithScrollButton_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.ScrollUp
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_TextInputTooLong_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = new string('x', EditorActionValidator.MaxTextInputLength + 1)
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("maximum length");
    }

    [Fact]
    public void Validate_DelayWithRandomBounds_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.Delay,
            UseRandomDelay = true,
            RandomDelayMinMs = 100,
            RandomDelayMaxMs = 250
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_DelayWithInvalidRandomBounds_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.Delay,
            UseRandomDelay = true,
            RandomDelayMinMs = 300,
            RandomDelayMaxMs = 100
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("maximum");
    }

    [Fact]
    public void ValidateAll_WhenMixedCoordinateModes_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 10 },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 1, Y = 1 }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }
}
