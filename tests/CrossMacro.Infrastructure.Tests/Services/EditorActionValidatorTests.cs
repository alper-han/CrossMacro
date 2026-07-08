using System;
using System.Reflection;
using CrossMacro.Core.Models;
using CrossMacro.Core.Resources;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
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
    public void Validate_TextInputWithLongMultilineContent_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = string.Join('\n', Enumerable.Repeat(new string('x', 1_000), 20))
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardGetWithValidVariable_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "clipText" };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardGetWithInvalidVariable_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "1clip" };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Clipboard destination variable");
    }

    [Fact]
    public void Validate_ClipboardSetWithText_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = "hello" };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardSetWithEmptyText_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = string.Empty };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Clipboard text");
    }

    [Theory]
    [InlineData("window", "window")]
    [InlineData("clipboard get", "clipboard")]
    [InlineData("shell", "shell")]
    [InlineData("pixelcolor", "pixelcolor")]
    [InlineData("waitcolor 1 2", "waitcolor")]
    [InlineData("pixelsearch 1 2 3 4", "pixelsearch")]
    public void Validate_RawScriptStepWithMalformedRecognizedCommand_ReturnsCommandSpecificError(string text, string expected)
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = text };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().ContainEquivalentOf(expected);
    }

    [Fact]
    public void Validate_RawScriptStepWithUnknownNonEmptyText_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = "custom raw text" };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_RawScriptStepWithEmptyText_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = string.Empty };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Raw script step cannot be empty");
    }

    [Fact]
    public void Validate_ShellCommandWithCommandAndOptions_ReturnsValid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = ShellCommandMode.ShellCaptureInput,
            ShellCommand = "cat",
            ShellStandardInput = "hello",
            ShellExitCodeVariableName = "_",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "stderr",
            ShellRetries = 1,
            ShellBackoffMs = 20,
            ShellTimeoutMs = 1000
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShellCommandWithInvalidCaptureTarget_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = ShellCommandMode.ShellCapture,
            ShellCommand = "echo ok",
            ShellExitCodeVariableName = "1exit",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "stderr"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("capture targets");
    }

    [Theory]
    [MemberData(nameof(ValidWindowActions))]
    public void Validate_WindowCommandWithValidMode_ReturnsValid(EditorAction action)
    {
        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_WindowCommandWithInvalidVariable_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = WindowCommandMode.Active,
            WindowActiveField = "title",
            WindowOutputVariable = "1bad"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid variable");
    }

    [Fact]
    public void Validate_WindowCommandWithMissingRequiredField_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = WindowCommandMode.Search,
            WindowSelectorKind = "title",
            WindowSelectorValue = string.Empty,
            WindowOutputVariable = "addr"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Search term");
    }

    public static IEnumerable<object[]> ValidWindowActions()
    {
        foreach (var mode in Enum.GetValues<WindowCommandMode>())
        {
            yield return [CreateValidWindowAction(mode)];
        }
    }

    private static EditorAction CreateValidWindowAction(WindowCommandMode mode)
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = mode is WindowCommandMode.Focus or WindowCommandMode.Close ? "active" : "title",
            WindowSelectorValue = mode == WindowCommandMode.WorkspaceMoveWindow ? "0x123" : "Firefox",
            WindowActiveField = "title",
            WindowOutputVariable = mode == WindowCommandMode.WorkspaceGet ? "workspaceName" : "windowAddress",
            WindowTimeoutMs = 2500,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2"
        };
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
    public void Validate_WaitColorWithInvalidResultVariable_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            ScreenX = 1,
            ScreenY = 2,
            ScreenColorHex = "00FF00",
            ScreenTimeoutMs = 100,
            ScreenColorVariableName = "1invalid"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("result variable");
    }

    [Fact]
    public void Validate_PixelSearchWithInvalidFoundVariable_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 0,
            ScreenTop = 0,
            ScreenWidth = 10,
            ScreenHeight = 10,
            ScreenColorHex = "00FF00",
            ScreenTolerance = 0,
            ScreenFoundVariableName = "1invalid",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("output variable");
    }

    [Theory]
    [MemberData(nameof(ValidScreenReadingActions))]
    public void Validate_ScreenReadingActionsWithStructuredPayload_ReturnsValid(EditorAction action)
    {
        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    public static IEnumerable<object[]> ValidScreenReadingActions()
    {
        yield return
        [
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = false,
                ScreenX = -5,
                ScreenY = 8,
                ScreenColorVariableName = "sample_color"
            }
        ];

        yield return
        [
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 5,
                ScreenY = 8,
                ScreenColorHex = "00FF00",
                ScreenTimeoutMs = 100,
                ScreenColorVariableName = "wait_ok"
            }
        ];

        yield return
        [
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 10,
                ScreenHeight = 10,
                ScreenColorHex = "00FF00",
                ScreenTolerance = 255,
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y"
            }
        ];
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void Validate_TargetColorSource_WhenManualModeAndHexIsInvalid_ReturnsInvalid(EditorActionType actionType)
    {
        var action = CreateScreenReadingAction(actionType);
        SetRequiredEnumPropertyValue(action, "ScreenTargetColorSource", "Manual");
        action.ScreenColorHex = "00GG00";

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("target");
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void Validate_TargetColorSource_WhenVariableModeAndNameIsInvalid_ReturnsInvalid(EditorActionType actionType)
    {
        var action = CreateScreenReadingAction(actionType);
        SetRequiredEnumPropertyValue(action, "ScreenTargetColorSource", "Variable");
        SetRequiredPropertyValue(action, "ScreenTargetColorVariableName", "1invalid");
        action.ScreenColorHex = "00FF00";

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("variable");
    }

    private static EditorAction CreateScreenReadingAction(EditorActionType actionType)
    {
        return actionType switch
        {
            EditorActionType.WaitColor => new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 1,
                ScreenY = 2,
                ScreenColorHex = "00FF00",
                ScreenTimeoutMs = 100,
                ScreenColorVariableName = "result"
            },
            EditorActionType.PixelSearch => new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 10,
                ScreenHeight = 10,
                ScreenColorHex = "00FF00",
                ScreenTolerance = 0,
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };
    }

    private static PropertyInfo GetRequiredProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Expected property '{propertyName}' on {target.GetType().Name}.");
    }

    private static void SetRequiredPropertyValue(object target, string propertyName, object? value)
    {
        GetRequiredProperty(target, propertyName).SetValue(target, value);
    }

    private static void SetRequiredEnumPropertyValue(object target, string propertyName, string enumName)
    {
        var property = GetRequiredProperty(target, propertyName);
        var value = Enum.Parse(property.PropertyType, enumName, ignoreCase: false);
        property.SetValue(target, value);
    }

    [Fact]
    public void ValidateAll_WhenMixedCoordinateModes_ReturnsValid()
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
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void ValidateAll_WhenMouseButtonModesAreMixed_ReturnsValid()
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
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
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
        result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustNotUseCoordinates);
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
        result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustNotUseCoordinates);
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
    public void Validate_ConditionWithDollarVariableAndLiteralDollarText_ReturnsValid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "$name",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Text,
            ScriptRightOperand = "$foo"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ConditionWithColorOperand_ReturnsValid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "1c1c1c"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ConditionWithInvalidColorOperand_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "GGGGGG"
        };

        var result = _validator.Validate(action);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Right operand");
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
