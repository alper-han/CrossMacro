
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorActionValidatorTests
{
    private readonly EditorActionValidator _validator;
    private readonly EditorActionValidator _scriptAwareValidator;

    public EditorActionValidatorTests()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.GetKeyCode("Shift").Returns(42);
        _ = keyCodeMapper.GetKeyCode("AltGr").Returns(100);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => call.Arg<char>());
        _ = keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(call => char.IsUpper(call.Arg<char>()));
        _ = keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);

        _validator = new EditorActionValidator(new EditorActionConverter(keyCodeMapper));
        _scriptAwareValidator = new EditorActionValidator(
            new EditorActionConverter(keyCodeMapper),
            new ScriptValidationService(keyCodeMapper));
    }

    [Fact]
    public void Validate_MouseButtonWithScrollButton_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.ScrollUp,
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_TextInputWithLongMultilineContent_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = string.Join('\n', Enumerable.Repeat(new string('x', 1_000), 20)),
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardGetWithValidVariable_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "clipText" };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardGetWithInvalidVariable_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "1clip" };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Clipboard destination variable");
    }

    [Fact]
    public void Validate_ClipboardSetWithText_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = "hello" };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ClipboardSetWithEmptyText_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = string.Empty };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Clipboard text");
    }

    [Fact]
    public void Validate_MousePositionWithDuplicateDestinations_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.MousePosition,
            MousePositionXVariableName = "cursor",
            MousePositionYVariableName = "cursor",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("must be different");
    }

    [Theory]
    [InlineData("window", "window")]
    [InlineData("clipboard get", "clipboard")]
    [InlineData("shell", "shell")]
    [InlineData("pixelcolor", "pixelcolor")]
    [InlineData("waitcolor 1 2", "waitcolor")]
    [InlineData("pixelsearch 1 2 3 4", "pixelsearch")]
    [InlineData("mouse position x x", "different")]
    public void Validate_RawScriptStepWithMalformedRecognizedCommand_ReturnsCommandSpecificError(string text, string expected)
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = text };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().ContainEquivalentOf(expected);
    }

    [Fact]
    public void Validate_RawScriptStepWithUnknownNonEmptyText_ReturnsValid()
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = "custom raw text" };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_RawScriptStepWithEmptyText_ReturnsInvalid()
    {
        var action = new EditorAction { Type = EditorActionType.RawScriptStep, Text = string.Empty };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Raw script step cannot be empty");
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
            ShellTimeoutMs = 1000,
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
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
            ShellStandardErrorVariableName = "stderr",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("capture targets");
    }

    [Theory]
    [MemberData(nameof(ValidWindowActions))]
    [MemberData(nameof(ValidScreenReadingActions))]
    public void Validate_StructuredActionWithValidPayload_ReturnsValid(EditorAction action)
    {
        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_WindowCommandWithInvalidVariable_ReturnsInvalid()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = WindowCommandMode.Active,
            WindowActiveField = "title",
            WindowOutputVariable = "1bad",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Invalid variable");
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
            WindowOutputVariable = "addr",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Search term");
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
            WindowSelectorValue = mode is WindowCommandMode.WorkspaceMoveWindow ? "0x123" : "Firefox",
            WindowActiveField = "title",
            WindowOutputVariable = mode is WindowCommandMode.WorkspaceGet ? "workspaceName" : "windowAddress",
            WindowTimeoutMs = 2500,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2",
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
            RandomDelayMaxMs = 250,
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
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
            RandomDelayMaxMs = 100,
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("maximum");
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
            ScreenColorVariableName = "1invalid",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("result variable");
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
            ScreenFoundYVariableName = "found_y",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("output variable");
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
                ScreenColorVariableName = "sample_color",
            },
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
                ScreenColorVariableName = "wait_ok",
            },
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
                ScreenFoundYVariableName = "found_y",
            },
        ];
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void Validate_TargetColorSource_WhenManualModeAndHexIsInvalid_ReturnsInvalid(EditorActionType actionType)
    {
        var action = CreateScreenReadingAction(actionType);
        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex;
        action.ScreenColorHex = "00GG00";

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("target");
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void Validate_TargetColorSource_WhenVariableModeAndNameIsInvalid_ReturnsInvalid(EditorActionType actionType)
    {
        var action = CreateScreenReadingAction(actionType);
        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable;
        action.ScreenTargetColorVariableName = "1invalid";
        action.ScreenColorHex = "00FF00";

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("variable");
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
                ScreenColorVariableName = "result",
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
                ScreenFoundYVariableName = "found_y",
            },
            EditorActionType.MouseMove => throw new NotSupportedException(),
            EditorActionType.MouseClick => throw new NotSupportedException(),
            EditorActionType.MouseDown => throw new NotSupportedException(),
            EditorActionType.MouseUp => throw new NotSupportedException(),
            EditorActionType.KeyPress => throw new NotSupportedException(),
            EditorActionType.KeyDown => throw new NotSupportedException(),
            EditorActionType.KeyUp => throw new NotSupportedException(),
            EditorActionType.Delay => throw new NotSupportedException(),
            EditorActionType.ScrollVertical => throw new NotSupportedException(),
            EditorActionType.ScrollHorizontal => throw new NotSupportedException(),
            EditorActionType.TextInput => throw new NotSupportedException(),
            EditorActionType.SetVariable => throw new NotSupportedException(),
            EditorActionType.IncrementVariable => throw new NotSupportedException(),
            EditorActionType.DecrementVariable => throw new NotSupportedException(),
            EditorActionType.MultiplyVariable => throw new NotSupportedException(),
            EditorActionType.DivideVariable => throw new NotSupportedException(),
            EditorActionType.RepeatBlockStart => throw new NotSupportedException(),
            EditorActionType.IfBlockStart => throw new NotSupportedException(),
            EditorActionType.ElseBlockStart => throw new NotSupportedException(),
            EditorActionType.WhileBlockStart => throw new NotSupportedException(),
            EditorActionType.ForBlockStart => throw new NotSupportedException(),
            EditorActionType.BlockEnd => throw new NotSupportedException(),
            EditorActionType.Break => throw new NotSupportedException(),
            EditorActionType.Continue => throw new NotSupportedException(),
            EditorActionType.PixelColor => throw new NotSupportedException(),
            EditorActionType.ImageSearch => throw new NotSupportedException(),
            EditorActionType.ImageClick => throw new NotSupportedException(),
            EditorActionType.WaitImage => throw new NotSupportedException(),
            EditorActionType.ClipboardGet => throw new NotSupportedException(),
            EditorActionType.ClipboardSet => throw new NotSupportedException(),
            EditorActionType.ShellCommand => throw new NotSupportedException(),
            EditorActionType.Screenshot => throw new NotSupportedException(),
            EditorActionType.WindowCommand => throw new NotSupportedException(),
            EditorActionType.RawScriptStep => throw new NotSupportedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, message: null),
        };
    }

    [Fact]
    public void ValidateAll_WhenMixedCoordinateModes_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 10 },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 1, Y = 1 },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void ValidateAll_WhenMouseButtonModesAreMixed_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseClick, IsAbsolute = true, X = 100, Y = 200, Button = MacroMouseButton.Left },
            new EditorAction { Type = EditorActionType.MouseDown, IsAbsolute = false, X = 0, Y = 0, Button = MacroMouseButton.Left },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
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
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
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
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
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
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().NotContain(e => e.Contains("Cannot mix Absolute and Relative coordinates"));
    }

    [Fact]
    public void Validate_CurrentPositionClickWithAbsoluteMode_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustNotUseCoordinates);
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
            Button = MacroMouseButton.Left,
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Be(ValidationMessages.CurrentPositionClickMustNotUseCoordinates);
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
            ScriptValue = "10",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
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
            ScriptValue = "10",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Variable name");
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
            ForStepValue = "1",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_RepeatBlockWithNegativeCount_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.Number,
            ScriptNumericValue = "-1",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain(">= 0");
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
            ForStepValue = "0",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("cannot be 0");
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
            ForStepValue = "1",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("variable reference");
        _ = result.Error.Should().Contain("not a number literal");
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
            ScriptRightOperand = "$foo",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
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
            ScriptRightOperand = "1c1c1c",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ImageSearchWithNonFiniteSimilarity_ReturnsInvalid()
    {
        foreach (var similarity in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var action = new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "Target",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
                ScreenTimeoutMs = 1500,
                ImageSearchSimilarity = similarity,
            };

            var result = _validator.Validate(action);

            _ = result.IsValid.Should().BeFalse();
            _ = result.Error.Should().Contain("similarity");
        }
    }

    [Fact]
    public void Validate_ImageSearchWithBoundarySimilarities_ReturnsValid()
    {
        foreach (var similarity in new[] { 0.0, 1.0 })
        {
            var action = new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "Target",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
                ScreenTimeoutMs = 1500,
                ImageSearchSimilarity = similarity,
            };

            var result = _validator.Validate(action);

            _ = result.IsValid.Should().BeTrue();
            _ = result.Error.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(MacroMouseButton.Side1)]
    [InlineData(MacroMouseButton.Side2)]
    public void Validate_ImageClickWithUnsupportedButton_ReturnsInvalid(MacroMouseButton button)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ImageClick,
            ScreenWidth = 30,
            ScreenHeight = 40,
            ImageAssetName = "Target",
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
            Button = button,
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("button");
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
            ScriptRightOperand = "GGGGGG",
        };

        var result = _validator.Validate(action);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().Contain("Right operand");
    }

    [Fact]
    public void ValidateAll_WhenElseNotAfterIfBlock_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error => error.Contains("else block", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAll_WhenIfElseStructureIsCorrect_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.IfBlockStart, ScriptLeftOperand = "1", ScriptLeftOperandType = ScriptOperandType.Number, ScriptRightOperand = "1", ScriptRightOperandType = ScriptOperandType.Number, ScriptConditionOperator = ScriptConditionOperator.Equals },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenFlowControlCurrentPositionThenAbsoluteMove_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAll_WhenFlowControlUsesAbsoluteBeforeButton_ReturnsValid()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = false, IsAbsolute = true, X = 100, Y = 100 },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenPixelSearchFeedsVariableMove_ReturnsValid()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 10,
                ScreenHeight = 10,
                ScreenColorHex = "00FF00",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "found",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Boolean,
                ScriptRightOperand = "true",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$found_x",
                CoordinateYToken = "$found_y",
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var result = _scriptAwareValidator.ValidateAll(actions);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAll_WhenVariableMoveUsesUndefinedVariable_ReturnsError()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$missing_x",
                CoordinateYToken = "100",
            },
        };

        var result = _scriptAwareValidator.ValidateAll(actions);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error =>
            error.Contains("unknown variable", StringComparison.OrdinalIgnoreCase)
            && error.Contains("missing_x", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateAll_WhenBreakUsedOutsideLoop_ReturnsValidationError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error =>
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
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_WhenContinueUsedOutsideLoop_ReturnsError()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error =>
            error.Contains("continue", StringComparison.OrdinalIgnoreCase)
            && error.Contains("inside repeat/while/for blocks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RepeatBlockWithExpressionCount_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "$count / 10",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_RepeatBlockWithMalformedExpressionCount_ReturnsInvalid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "$count /",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_ForBlockWithExpressionSegments_ReturnsValid()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "$n + 1",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "$s - 1",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeTrue();
        _ = result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_ForBlockWithMalformedStepExpression_ReturnsInvalid()
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
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "$s +",
        };

        // Act
        var result = _validator.Validate(action);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidateAll_WhenScriptUsesArithmeticBlockArguments_CompilesClean()
    {
        // Arrange: expression values flow through ToMacroSequence -> RunScriptCompiler
        // (the plain validator compiles structured script actions during ValidateAll).
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "count",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "20",
            },
            new EditorAction
            {
                Type = EditorActionType.RepeatBlockStart,
                ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
                ScriptNumericValue = "$count / 10",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "0",
                ForEndType = ScriptNumericSourceType.VariableReference,
                ForEndValue = "$count / 10",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var result = _validator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeTrue(string.Join(" | ", result.Errors));
    }

    [Fact]
    public void ValidateAll_WhenScriptUsesMalformedRepeatExpression_ReportsCanonicalMessage()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RawScriptStep, Text = "repeat $count / {" },
            new EditorAction { Type = EditorActionType.RawScriptStep, Text = "click left" },
            new EditorAction { Type = EditorActionType.RawScriptStep, Text = "}" },
        };

        // Act
        var result = _scriptAwareValidator.ValidateAll(actions);

        // Assert
        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error => error.Contains("is not a valid numeric expression for repeat count.", StringComparison.Ordinal));
    }
}
