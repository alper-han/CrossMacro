using CrossMacro.Core.Models;
using CrossMacro.Core.Resources;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public class EditorActionValidatorTests
{
    private readonly EditorActionValidator _validator;

    public EditorActionValidatorTests()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.GetKeyCode("Shift").Returns(42);
        keyCodeMapper.GetKeyCode("AltGr").Returns(100);
        keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => call.Arg<char>());
        keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(call => char.IsUpper(call.Arg<char>()));
        keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(false);

        _validator = new EditorActionValidator(new EditorActionConverter(keyCodeMapper));
    }

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

    [Fact]
    public void ValidateAll_WhenMouseButtonModesAreMixed_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseClick, IsAbsolute = true, X = 100, Y = 200, Button = MouseButton.Left },
            new EditorAction { Type = EditorActionType.MouseDown, IsAbsolute = false, X = 0, Y = 0, Button = MouseButton.Left }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void ValidateAll_WhenAbsoluteActionsIncludeCurrentPositionClick_DoesNotReturnMixedModeError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void ValidateAll_WhenAbsoluteActionsIncludeCurrentPositionMouseDown_DoesNotReturnMixedModeError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction
            {
                Type = EditorActionType.MouseDown,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void ValidateAll_WhenAbsoluteActionsIncludeCurrentPositionMouseUp_DoesNotReturnMixedModeError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction
            {
                Type = EditorActionType.MouseUp,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void Validate_CurrentPositionClickWithAbsoluteMode_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustBeRelative);
    }

    [Fact]
    public void Validate_CurrentPositionMouseDownWithAbsoluteMode_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseDown,
            IsAbsolute = true,
            UseCurrentPosition = true,
            Button = MouseButton.Left
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustBeRelative);
    }

    [Fact]
    public void Validate_SetVariableWithStructuredFields_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            ScriptVariableName = "count",
            ScriptValueType = ScriptValueType.Number,
            ScriptValue = "10"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_SetVariableWithInvalidVariableName_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            ScriptVariableName = "1count",
            ScriptValueType = ScriptValueType.Number,
            ScriptValue = "10"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Variable name");
    }

    [Fact]
    public void Validate_ForBlockWithStructuredFields_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "maxCount",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.Number,
            ForStepValue = "1"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_RepeatBlockWithNegativeCount_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.Number,
            ScriptNumericValue = "-1"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain(">= 0");
    }

    [Fact]
    public void Validate_ForBlockWithExplicitZeroStep_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.Number,
            ForEndValue = "10",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.Number,
            ForStepValue = "0"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("cannot be 0");
    }

    [Fact]
    public void Validate_ForBlockWithVariableModeAndNumericLiteral_ReturnsSpecificVariableNameError()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.VariableReference,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "10",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "1"
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("variable reference");
        result.Error.Should().Contain("not a number literal");
    }

    [Fact]
    public void ValidateAll_WhenElseNotAfterIfBlock_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("else block", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAll_WhenIfElseStructureIsCorrect_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.IfBlockStart, ScriptLeftOperand = "1", ScriptLeftOperandType = ScriptOperandType.Number, ScriptRightOperand = "1", ScriptRightOperandType = ScriptOperandType.Number, ScriptConditionOperator = ScriptConditionOperator.Equals },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenFlowControlCurrentPositionThenAbsoluteMove_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAll_WhenFlowControlUsesAbsoluteBeforeButton_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = false, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenBreakUsedOutsideLoop_ReturnsValidationError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.Contains("break", StringComparison.OrdinalIgnoreCase)
            && error.Contains("inside repeat/while/for blocks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAll_WhenContinueUsedInsideLoop_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenContinueUsedOutsideLoop_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false }
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.Contains("continue", StringComparison.OrdinalIgnoreCase)
            && error.Contains("inside repeat/while/for blocks", StringComparison.OrdinalIgnoreCase));
    }
}
