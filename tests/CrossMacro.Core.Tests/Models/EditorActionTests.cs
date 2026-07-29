
namespace CrossMacro.Core.Tests.Models;

public sealed class EditorActionTests
{
    [Fact]
    public void CommandPayloads_ProjectOnlyTheirOwnedEditorFields()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "capture.png",
            ScreenshotCopyToClipboard = true,
            ScreenshotUseRegion = true,
            ScreenshotRegionX = "$x",
            ScreenshotRegionY = "2",
            ScreenshotRegionWidth = "30",
            ScreenshotRegionHeight = "40",
            ShellCommand = "echo ignored",
            WindowSelectorValue = "ignored",
        };

        _ = action.TryGetScreenshotPayload(out var screenshot).Should().BeTrue();
        _ = screenshot.OutputPath.Should().Be("capture.png");
        _ = screenshot.RegionWidth.Should().Be("30");
        _ = action.TryGetShellPayload(out _).Should().BeFalse();
        _ = action.TryGetWindowPayload(out _).Should().BeFalse();
    }

    [Fact]
    public void CommandPayloads_RejectWrongActionTypeWithoutChangingTheFacade()
    {
        var action = new EditorAction { Type = EditorActionType.ShellCommand, ShellCommand = "echo ok" };

        _ = action.TryGetShellPayload(out var shell).Should().BeTrue();
        _ = shell.Command.Should().Be("echo ok");
        _ = action.TryGetScreenshotPayload(out _).Should().BeFalse();
        _ = action.TryGetWindowPayload(out _).Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesNewActionWithCopiedFields()
    {
        // Arrange
        var source = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            X = 40,
            Y = 55,
            IsAbsolute = false,
            Button = MacroMouseButton.Right,
            KeyCode = 30,
            KeyName = "A",
            DelayMs = 25,
            UseRandomDelay = true,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            ScrollAmount = -2,
            Text = "hello",
            ScriptVariableName = "counter",
            ScriptValueType = ScriptValueType.Number,
            ScriptValue = "42",
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "stepAmount",
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "counter",
            ScriptConditionOperator = ScriptConditionOperator.LessThanOrEqual,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "100",
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.Number,
            ForEndValue = "10",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.Number,
            ForStepValue = "2",
            ShellCommandMode = ShellCommandMode.ShellCaptureInput,
            ShellCommand = "cat",
            ShellStandardInput = "hello",
            ShellExitCodeVariableName = "exitVar",
            ShellStandardOutputVariableName = "outVar",
            ShellStandardErrorVariableName = "errVar",
            ShellRetries = 2,
            ShellBackoffMs = 50,
            ShellTimeoutMs = 1000,
            WindowCommandMode = WindowCommandMode.Wait,
            WindowSelectorKind = "class",
            WindowSelectorValue = "Firefox",
            WindowActiveField = "address",
            WindowOutputVariable = "windowAddr",
            WindowTimeoutMs = 2500,
            WindowX = 10,
            WindowY = 20,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2",
        };

        // Act
        var clone = source.Clone();

        // Assert
        _ = clone.Should().NotBeSameAs(source);
        _ = clone.Id.Should().NotBe(source.Id);
        _ = clone.Type.Should().Be(source.Type);
        _ = clone.X.Should().Be(source.X);
        _ = clone.Y.Should().Be(source.Y);
        _ = clone.IsAbsolute.Should().Be(source.IsAbsolute);
        _ = clone.Button.Should().Be(source.Button);
        _ = clone.KeyCode.Should().Be(source.KeyCode);
        _ = clone.KeyName.Should().Be(source.KeyName);
        _ = clone.DelayMs.Should().Be(source.DelayMs);
        _ = clone.UseRandomDelay.Should().Be(source.UseRandomDelay);
        _ = clone.RandomDelayMinMs.Should().Be(source.RandomDelayMinMs);
        _ = clone.RandomDelayMaxMs.Should().Be(source.RandomDelayMaxMs);
        _ = clone.ScrollAmount.Should().Be(source.ScrollAmount);
        _ = clone.Text.Should().Be(source.Text);
        _ = clone.ScriptVariableName.Should().Be(source.ScriptVariableName);
        _ = clone.ScriptValueType.Should().Be(source.ScriptValueType);
        _ = clone.ScriptValue.Should().Be(source.ScriptValue);
        _ = clone.ScriptNumericSourceType.Should().Be(source.ScriptNumericSourceType);
        _ = clone.ScriptNumericValue.Should().Be(source.ScriptNumericValue);
        _ = clone.ScriptLeftOperandType.Should().Be(source.ScriptLeftOperandType);
        _ = clone.ScriptLeftOperand.Should().Be(source.ScriptLeftOperand);
        _ = clone.ScriptConditionOperator.Should().Be(source.ScriptConditionOperator);
        _ = clone.ScriptRightOperandType.Should().Be(source.ScriptRightOperandType);
        _ = clone.ScriptRightOperand.Should().Be(source.ScriptRightOperand);
        _ = clone.ForVariableName.Should().Be(source.ForVariableName);
        _ = clone.ForStartType.Should().Be(source.ForStartType);
        _ = clone.ForStartValue.Should().Be(source.ForStartValue);
        _ = clone.ForEndType.Should().Be(source.ForEndType);
        _ = clone.ForEndValue.Should().Be(source.ForEndValue);
        _ = clone.ForHasStep.Should().Be(source.ForHasStep);
        _ = clone.ForStepType.Should().Be(source.ForStepType);
        _ = clone.ForStepValue.Should().Be(source.ForStepValue);
        _ = clone.ShellCommandMode.Should().Be(source.ShellCommandMode);
        _ = clone.ShellCommand.Should().Be(source.ShellCommand);
        _ = clone.ShellStandardInput.Should().Be(source.ShellStandardInput);
        _ = clone.ShellExitCodeVariableName.Should().Be(source.ShellExitCodeVariableName);
        _ = clone.ShellStandardOutputVariableName.Should().Be(source.ShellStandardOutputVariableName);
        _ = clone.ShellStandardErrorVariableName.Should().Be(source.ShellStandardErrorVariableName);
        _ = clone.ShellRetries.Should().Be(source.ShellRetries);
        _ = clone.ShellBackoffMs.Should().Be(source.ShellBackoffMs);
        _ = clone.ShellTimeoutMs.Should().Be(source.ShellTimeoutMs);
        _ = clone.WindowCommandMode.Should().Be(source.WindowCommandMode);
        _ = clone.WindowSelectorKind.Should().Be(source.WindowSelectorKind);
        _ = clone.WindowSelectorValue.Should().Be(source.WindowSelectorValue);
        _ = clone.WindowActiveField.Should().Be(source.WindowActiveField);
        _ = clone.WindowOutputVariable.Should().Be(source.WindowOutputVariable);
        _ = clone.WindowTimeoutMs.Should().Be(source.WindowTimeoutMs);
        _ = clone.WindowX.Should().Be(source.WindowX);
        _ = clone.WindowY.Should().Be(source.WindowY);
        _ = clone.WindowWidth.Should().Be(source.WindowWidth);
        _ = clone.WindowHeight.Should().Be(source.WindowHeight);
        _ = clone.WindowWorkspace.Should().Be(source.WindowWorkspace);
    }

    [Fact]
    public void DisplayName_ForTextInput_TruncatesLongText()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "abcdefghijklmnopqrstuvwxyz",
        };

        // Act
        var display = action.DisplayName;

        // Assert
        _ = display.Should().Contain("Type");
        _ = display.Should().Contain("...");
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidDelayAndScroll()
    {
        // Arrange
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = -1 };
        var randomDelay = new EditorAction
        {
            Type = EditorActionType.Delay,
            UseRandomDelay = true,
            RandomDelayMinMs = 200,
            RandomDelayMaxMs = 100,
        };
        var scroll = new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 0 };

        // Assert
        _ = delay.IsValid().Should().BeFalse();
        _ = randomDelay.IsValid().Should().BeFalse();
        _ = scroll.IsValid().Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public void IsValid_ImageActionsUseStructuredFields(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScreenWidth = EditorActionScreenReadingPayload.DefaultSearchScreenWidth,
            ScreenHeight = EditorActionScreenReadingPayload.DefaultSearchScreenHeight,
            ImageAssetName = "Target",
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
            ScreenTimeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs,
            ImageSearchSimilarity = EditorActionScreenReadingPayload.DefaultImageSearchSimilarity,
            ImageSearchDownsample = EditorActionScreenReadingPayload.DefaultImageSearchDownsample,
            Button = MacroMouseButton.Left,
        };

        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_ImageClickRejectsSideButtons()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ImageClick,
            ScreenWidth = 100,
            ScreenHeight = 100,
            ImageAssetName = "Target",
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
            Button = MacroMouseButton.Side1,
        };

        _ = action.IsValid().Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public void ScreenReadingPayload_CoversImageActions(EditorActionType actionType)
    {
        _ = EditorActionScreenReadingPayload.TryCreateDefault(actionType, out var defaults).Should().BeTrue();
        _ = defaults.ScreenWidth.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenWidth);
        _ = defaults.ScreenHeight.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenHeight);
        _ = defaults.ImageSearchSimilarity.Should().Be(1.0);
        _ = defaults.ImageSearchDownsample.Should().Be(1);
        _ = defaults.Button.Should().Be(MacroMouseButton.Left);

        var action = new EditorAction
        {
            Type = actionType,
            ImageAssetName = "Target",
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        };

        _ = action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        _ = payload.OutputVariableNames.Should().Equal("found", "found_x", "found_y");
        _ = payload.GetOutputVariableRole("found").Should().Be(EditorActionScreenReadingVariableRole.Boolean);
        _ = payload.GetOutputVariableRole("found_x").Should().Be(EditorActionScreenReadingVariableRole.Number);
        _ = payload.GetOutputVariableRole("found_y").Should().Be(EditorActionScreenReadingVariableRole.Number);
    }

    [Fact]
    public void IsValid_WhenTextInputContainsOnlyWhitespaceOrLineBreaks_ReturnsTrue()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = " \n\t",
        };

        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenTextInputIsEmpty_ReturnsFalse()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = string.Empty,
        };

        _ = action.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenScriptVariableReferencesUseDollarPrefix_ReturnsTrue()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            ScriptVariableName = "$target",
            ScriptValueType = ScriptValueType.VariableReference,
            ScriptValue = "$source",
        };

        // Act + Assert
        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void DisplayName_WhenForVariableValuesUseDollarPrefix_DoesNotDuplicateDollar()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.VariableReference,
            ForStartValue = "$start",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "$finish",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "$step",
        };

        // Act
        var displayName = action.DisplayName;

        // Assert
        _ = displayName.Should().Contain("$start");
        _ = displayName.Should().Contain("$finish");
        _ = displayName.Should().Contain("$step");
        _ = displayName.Should().NotContain("$$");
    }

    [Fact]
    public void DisplayName_WhenConditionTextOperandsUseDollarPrefix_EscapesLiteralDollarPreview()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Text,
            ScriptLeftOperand = "$foo",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.VariableReference,
            ScriptRightOperand = "$bar",
        };

        _ = action.DisplayName.Should().Contain("$$foo == $bar");
    }

    [Theory]
    [InlineData("1c1c1c")]
    [InlineData("1C1C1C")]
    [InlineData("00ff00")]
    public void ValidateOperandToken_WhenColorIsValidRgbHex_ReturnsTrue(string value)
    {
        _ = EditorActionScriptTokens.ValidateOperandToken(ScriptOperandType.Color, value).Should().BeTrue();
    }

    [Theory]
    [InlineData("1C1C1")]
    [InlineData("1C1C1C1")]
    [InlineData("GGGGGG")]
    [InlineData("")]
    public void ValidateOperandToken_WhenColorIsInvalidRgbHex_ReturnsFalse(string value)
    {
        _ = EditorActionScriptTokens.ValidateOperandToken(ScriptOperandType.Color, value).Should().BeFalse();
    }

    [Fact]
    public void FormatOperandToken_WhenColorUsesLowercaseHex_ReturnsUppercaseRgbHex()
    {
        _ = EditorActionScriptTokens.FormatOperandToken(ScriptOperandType.Color, "1c2d3e").Should().Be("1C2D3E");
    }

    [Fact]
    public void IsValid_WhenNumericVariableReferenceUsesDollarPrefix_ReturnsTrue()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "$count",
        };

        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenShellCaptureUsesIgnoredTargets_ReturnsTrue()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = ShellCommandMode.ShellCapture,
            ShellCommand = "echo ok",
            ShellExitCodeVariableName = "_",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "_",
        };

        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ImageSearchMutations_ClearLegacyScriptPreference()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ImageSearch,
            Text = "legacy image search",
        };

        action.SetImageSearchScaleAware(value: true);
        action.SetImageSearchMatchMode(EditorImageMatchMode.BestMatch);

        _ = action.ImageSearchScaleAware.Should().BeTrue();
        _ = action.ImageSearchMatchMode.Should().Be(EditorImageMatchMode.BestMatch);
        _ = action.ImageSearchMatchModeWasExplicit.Should().BeTrue();
        _ = action.PreferLegacyScriptText.Should().BeFalse();
    }
}
