using CrossMacro.Core.Models.Macros;
using CrossMacro.Core.Models.Scripting;
using CrossMacro.Core.Services.Scripting;
using CrossMacro.Editing.Models;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Editing.Tests;

public sealed class EditorActionTests
{
    [Fact]
    public void DefaultScreenState_UsesTheNamedScreenReadingPayloadDefaults()
    {
        var action = new EditorAction();

        _ = action.ScreenColorHex.Should().Be(EditorActionScreenReadingPayload.DefaultColorHex);
        _ = action.ScreenColorVariableName.Should().Be(EditorActionScreenReadingPayload.DefaultColorVariableName);
        _ = action.ScreenTimeoutMs.Should().Be(EditorActionScreenReadingPayload.DefaultTimeoutMs);
        _ = action.ScreenTolerance.Should().Be(EditorActionScreenReadingPayload.DefaultTolerance);
        _ = action.ScreenWidth.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenWidth);
        _ = action.ScreenHeight.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenHeight);
        _ = action.ImageSearchSimilarity.Should().Be(EditorActionScreenReadingPayload.DefaultImageSearchSimilarity);
    }

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

        _ = action.TryGetScreenshotStep(out var screenshot).Should().BeTrue();
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
        _ = action.TryGetScreenshotStep(out _).Should().BeFalse();
        _ = action.TryGetWindowPayload(out _).Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesNewActionWithCopiedFields()
    {
        var source = new EditorAction
        {
            Comment = "step comment",
            Type = EditorActionType.MouseMove,
            X = 40,
            Y = 55,
            CoordinateXToken = "$found_x",
            CoordinateYToken = "$found_y",
            IsAbsolute = false,
            CoordinateSpace = MouseCoordinateSpace.RawDevice,
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
            MousePositionXVariableName = "mouse_x",
            MousePositionYVariableName = "mouse_y",
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

        var clone = source.Clone();

        _ = clone.Should().NotBeSameAs(source);
        _ = clone.Id.Should().NotBe(source.Id);
        _ = clone.Should().BeEquivalentTo(source, options => options.Excluding(action => action.Id));
    }

    [Fact]
    public void Clone_IsDetachedFromTheSourceState()
    {
        var source = new EditorAction { Type = EditorActionType.ShellCommand, ShellCommand = "echo", Comment = "note" };

        var clone = source.Clone();
        clone.ShellCommand = "changed";
        clone.Comment = "changed";

        _ = source.ShellCommand.Should().Be("echo");
        _ = source.Comment.Should().Be("note");
    }

    [Fact]
    public void Setters_RaiseOnePropertyChangedPerRealChangeAndNoneForRepeatedValues()
    {
        var action = new EditorAction { Type = EditorActionType.SetVariable };
        var changes = new List<string?>();
        action.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        action.ScriptVariableName = " counter ";
        action.ScriptVariableName = "counter";
        action.Comment = "note";
        action.Comment = "note";
        var id = action.Id;
        action.Id = id;

        _ = changes.Should().Equal(nameof(EditorAction.ScriptVariableName), nameof(EditorAction.Comment));
    }

    [Fact]
    public void StructuredScriptEdit_ClearsLegacyTextPreferenceBeforeNotifying()
    {
        var action = new EditorAction { Type = EditorActionType.SetVariable, Text = "x 1" };
        _ = action.PreferLegacyScriptText.Should().BeTrue();
        bool? preferenceWhenNotified = null;
        action.PropertyChanged += (_, _) => preferenceWhenNotified = action.PreferLegacyScriptText;

        action.ScriptValue = "7";

        _ = preferenceWhenNotified.Should().BeFalse();
    }

    [Fact]
    public void PreservedTextInputEvents_AreAvailableOnlyWhileTextInputPayloadIsUnchanged()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "hello",
        };
        var events = new List<MacroEvent>
        {
            new() { Type = EventType.KeyPress, KeyCode = 30 },
            new() { Type = EventType.KeyRelease, KeyCode = 30 },
        };

        action.PreserveTextInputEvents(events);

        _ = action.GetPreservedTextInputEvents().Should().Equal(events);

        action.Text = "changed";
        _ = action.GetPreservedTextInputEvents().Should().BeNull();

        action.Text = "hello";
        action.PreserveTextInputEvents(events);
        action.Type = EditorActionType.Delay;
        _ = action.GetPreservedTextInputEvents().Should().BeNull();
    }

    [Fact]
    public void CoordinateTokens_WhenSwitchingBetweenVariableAndLiteral_KeepNumericFacadeConsistent()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            X = 10,
            Y = 20,
            CoordinateXToken = "$found_x",
            CoordinateYToken = "42",
        };

        _ = action.CoordinateXToken.Should().Be("$found_x");
        _ = action.CoordinateYToken.Should().Be("42");
        _ = action.Y.Should().Be(42);
        _ = action.HasVariableCoordinates.Should().BeTrue();
        _ = action.TryGetLiteralCoordinates(out _, out _).Should().BeFalse();

        action.X = 7;

        _ = action.CoordinateXToken.Should().Be("7");
        _ = action.HasVariableCoordinates.Should().BeFalse();
        _ = action.TryGetLiteralCoordinates(out var x, out var y).Should().BeTrue();
        _ = x.Should().Be(7);
        _ = y.Should().Be(42);
    }

    [Fact]
    public void ImageSearchRegionTokens_WhenUsingVariables_AreCloned()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ImageSearch,
            ImageAssetName = "Button",
            ImageSearchRegionLeftToken = "$mouse_x",
            ImageSearchRegionTopToken = "$mouse_y",
            ImageSearchRegionWidthToken = "400",
            ImageSearchRegionHeightToken = "$height",
        };

        var clone = action.Clone();

        _ = clone.ImageSearchRegionLeftToken.Should().Be("$mouse_x");
        _ = clone.ImageSearchRegionTopToken.Should().Be("$mouse_y");
        _ = clone.ImageSearchRegionWidthToken.Should().Be("400");
        _ = clone.ImageSearchRegionHeightToken.Should().Be("$height");
        _ = clone.TryGetLiteralImageSearchRegion(out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void IsAbsolute_WhenModeChanges_PreservesExplicitLogicalRelativeCoordinates()
    {
        var action = new EditorAction
        {
            IsAbsolute = false,
            CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
        };

        action.IsAbsolute = true;
        action.IsAbsolute = false;

        _ = action.CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
    }

    [Fact]
    public void CoordinateSpace_WhenNewRelativeActionCreated_DefaultsToRawDevice()
    {
        var action = new EditorAction { IsAbsolute = false };

        _ = action.CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
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
        _ = defaults.ImageSearchSimilarity.Should().Be(0.95);
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

    [Theory]
    [InlineData(EditorActionType.PixelColor, EditorActionScreenReadingVariableRole.Color)]
    [InlineData(EditorActionType.WaitColor, EditorActionScreenReadingVariableRole.Boolean)]
    public void GetOutputVariableRole_ForColorReadingActions_UsesColorVariableOnly(
        EditorActionType actionType,
        EditorActionScreenReadingVariableRole expected)
    {
        var action = new EditorAction { Type = actionType, ScreenColorVariableName = "pixel", ScreenFoundVariableName = "found" };

        _ = action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        _ = payload.GetOutputVariableRole("pixel").Should().Be(expected);
        _ = payload.GetOutputVariableRole("found").Should().Be(EditorActionScreenReadingVariableRole.None);
    }

    [Fact]
    public void GetOutputVariableRole_ForPixelSearch_ClassifiesFoundAndCoordinateVariables()
    {
        var action = new EditorAction { Type = EditorActionType.PixelSearch, ScreenColorVariableName = "pixel" };

        _ = action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        _ = payload.GetOutputVariableRole("found").Should().Be(EditorActionScreenReadingVariableRole.Boolean);
        _ = payload.GetOutputVariableRole("found_x").Should().Be(EditorActionScreenReadingVariableRole.Number);
        _ = payload.GetOutputVariableRole("pixel").Should().Be(EditorActionScreenReadingVariableRole.None);
    }

    [Fact]
    public void TryCreateDefault_ForNonScreenReadingAction_ReturnsFalse()
    {
        _ = EditorActionScreenReadingPayload.TryCreateDefault(EditorActionType.Delay, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("$ target")]
    [InlineData("target value")]
    public void ScriptTokens_WhenVariableContainsWhitespace_ReturnFalse(string token)
    {
        _ = ScriptIdentifierSyntax.IsValidVariableName(token).Should().BeFalse();
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

    [Theory]
    [InlineData("count")]
    [InlineData("$count")]
    public void IsValidVariableName_WhenTokenHasValidName_ReturnsTrue(string token)
    {
        _ = ScriptIdentifierSyntax.IsValidVariableName(token).Should().BeTrue();
    }

    [Theory]
    [InlineData("$ x")]
    [InlineData("count value")]
    [InlineData("$1count")]
    public void IsValidVariableName_WhenTokenContainsWhitespaceOrInvalidStart_ReturnsFalse(string token)
    {
        _ = ScriptIdentifierSyntax.IsValidVariableName(token).Should().BeFalse();
    }

    [Fact]
    public void ImageSearchMutations_ClearLegacyScriptPreference()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ImageSearch,
            Text = "legacy image search",
        };

        action.SetImageSearchMatchMode(EditorImageMatchMode.BestMatch);

        _ = action.ImageSearchMatchMode.Should().Be(EditorImageMatchMode.BestMatch);
        _ = action.ImageSearchMatchModeWasExplicit.Should().BeTrue();
        _ = action.PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public void IsScriptStateAction_ClassifiesMulDivAsStateActions()
    {
        _ = EditorActionScriptClassifier.IsScriptStateAction(EditorActionType.MultiplyVariable).Should().BeTrue();
        _ = EditorActionScriptClassifier.IsScriptStateAction(EditorActionType.DivideVariable).Should().BeTrue();
        _ = EditorActionScriptClassifier.IsScriptFlowControlAction(EditorActionType.MultiplyVariable).Should().BeFalse();
        _ = EditorActionScriptClassifier.IsRuntimeEventAction(EditorActionType.DivideVariable).Should().BeFalse();
    }

}
