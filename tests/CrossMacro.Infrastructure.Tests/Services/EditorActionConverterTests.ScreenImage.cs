namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{

    [Theory]
    [InlineData(EditorImageMatchMode.Automatic, "auto")]
    [InlineData(EditorImageMatchMode.FirstThresholdMatch, "first")]
    [InlineData(EditorImageMatchMode.BestMatch, "best")]
    public void ToAndFromMacroSequence_WhenImageMatchModeIsExplicit_RoundTripsItsScriptToken(EditorImageMatchMode matchMode, string token)
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 1,
                ScreenTop = 2,
                ScreenWidth = 3,
                ScreenHeight = 4,
                ImageAssetName = "Target",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
                ImageSearchMatchMode = matchMode,
                ImageSearchMatchModeWasExplicit = true,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Match Mode", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal($"imagesearch 1 2 4 6 Target found found_x found_y similarity 0.95 matchmode {token}");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].ImageSearchMatchMode.Should().Be(matchMode);
        _ = restored.Actions[0].ImageSearchMatchModeWasExplicit.Should().BeTrue();
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredConditionUsesColorOperand_EmitsUppercaseBareHex()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = true,
                ScreenX = 1,
                ScreenY = 2,
                ScreenColorVariableName = "color",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "color",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Color,
                ScriptRightOperand = "1c1c1c",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Color", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(
            "pixelcolor 1 2 color",
            "if $color == 1C1C1C {",
            "click current left",
            "}");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingStepsPresent_RestoresStructuredActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 10 20 color",
                "pixelcolor rel -1 2 relativeColor",
                "waitcolor 11 22 00FFAA 2500 wait_ok",
                "pixelsearch 0 0 3 3 123456 found x y tolerance 5",
            },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().HaveCount(4);
        _ = result.Actions[0].Type.Should().Be(EditorActionType.PixelColor);
        _ = result.Actions[0].ScreenX.Should().Be(10);
        _ = result.Actions[0].ScreenY.Should().Be(20);
        _ = result.Actions[0].ScreenColorVariableName.Should().Be("color");
        _ = result.Actions[1].Type.Should().Be(EditorActionType.PixelColor);
        _ = result.Actions[1].IsAbsolute.Should().BeFalse();
        _ = result.Actions[1].ScreenX.Should().Be(-1);
        _ = result.Actions[1].ScreenY.Should().Be(2);
        _ = result.Actions[1].ScreenColorVariableName.Should().Be("relativeColor");
        _ = result.Actions[2].Type.Should().Be(EditorActionType.WaitColor);
        _ = result.Actions[2].ScreenX.Should().Be(11);
        _ = result.Actions[2].ScreenY.Should().Be(22);
        _ = result.Actions[2].ScreenColorHex.Should().Be("00FFAA");
        _ = result.Actions[2].ScreenTimeoutMs.Should().Be(2500);
        _ = result.Actions[2].ScreenColorVariableName.Should().Be("wait_ok");
        _ = result.Actions[3].Type.Should().Be(EditorActionType.PixelSearch);
        _ = result.Actions[3].ScreenLeft.Should().Be(0);
        _ = result.Actions[3].ScreenTop.Should().Be(0);
        _ = result.Actions[3].ScreenWidth.Should().Be(3);
        _ = result.Actions[3].ScreenHeight.Should().Be(3);
        _ = result.Actions[3].ScreenColorHex.Should().Be("123456");
        _ = result.Actions[3].ScreenFoundVariableName.Should().Be("found");
        _ = result.Actions[3].ScreenFoundXVariableName.Should().Be("x");
        _ = result.Actions[3].ScreenFoundYVariableName.Should().Be("y");
        _ = result.Actions[3].ScreenTolerance.Should().Be(5);
    }

    [Fact]
    public void ToMacroSequence_WhenScreenReadingActionsPresent_SerializesStructuredPayloads()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = true,
                ScreenX = 10,
                ScreenY = 20,
                ScreenTimeoutMs = 1200,
                ScreenColorVariableName = "color",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = false,
                ScreenX = -1,
                ScreenY = 2,
                ScreenTimeoutMs = 1300,
                ScreenColorVariableName = "relativeColor",
            },
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 11,
                ScreenY = 22,
                ScreenColorHex = "00ffaa",
                ScreenTimeoutMs = 2500,
                ScreenColorVariableName = "wait_ok",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 3,
                ScreenHeight = 3,
                ScreenColorHex = "123456",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "x",
                ScreenFoundYVariableName = "y",
                ScreenTimeoutMs = 1400,
                ScreenTolerance = 5,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "pixelcolor 10 20 color",
            "pixelcolor rel -1 2 relativeColor",
            "waitcolor 11 22 00FFAA 2500 wait_ok",
            "pixelsearch 0 0 3 3 123456 found x y timeout 1400 tolerance 5");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenImageSearchActionPresent_PreservesStructuredPayload()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "Target_1",
                ScreenFoundVariableName = "foundTarget",
                ScreenFoundXVariableName = "targetX",
                ScreenFoundYVariableName = "targetY",
                ScreenTimeoutMs = 1500,
                ImageSearchSimilarity = 0.875,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Search", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "imagesearch 10 20 40 60 Target_1 foundTarget targetX targetY similarity 0.875");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.ImageSearch);
        _ = restored.Actions[0].ScreenLeft.Should().Be(10);
        _ = restored.Actions[0].ScreenTop.Should().Be(20);
        _ = restored.Actions[0].ScreenWidth.Should().Be(30);
        _ = restored.Actions[0].ScreenHeight.Should().Be(40);
        _ = restored.Actions[0].ImageAssetName.Should().Be("Target_1");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("foundTarget");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("targetX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("targetY");
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(EditorActionScreenReadingPayload.DefaultTimeoutMs);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.875);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenImageSearchUsesNewDefaults_UsesAutomaticProfileWithoutTechnicalTokens()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "Target_1",
                ScreenFoundVariableName = "foundTarget",
                ScreenFoundXVariableName = "targetX",
                ScreenFoundYVariableName = "targetY",
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Automatic Image Search", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "imagesearch 10 20 40 60 Target_1 foundTarget targetX targetY similarity 0.95");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].ImageSearchMatchMode.Should().Be(EditorImageMatchMode.Automatic);
        _ = restored.Actions[0].ImageSearchMatchModeWasExplicit.Should().BeFalse();
    }

    [Theory]
    [InlineData(MacroMouseButton.Left, "left")]
    [InlineData(MacroMouseButton.Right, "right")]
    [InlineData(MacroMouseButton.Middle, "middle")]
    public void ToAndFromMacroSequence_WhenImageClickActionPresent_PreservesStructuredPayload(MacroMouseButton button, string buttonToken)
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageClick,
                Button = button,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "ButtonAsset",
                ScreenFoundVariableName = "clicked",
                ScreenFoundXVariableName = "clickX",
                ScreenFoundYVariableName = "clickY",
                ScreenTimeoutMs = 1600,
                ImageSearchSimilarity = 0.75,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Click", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            $"imageclick 10 20 40 60 ButtonAsset clicked clickX clickY button {buttonToken} timeout 1600 similarity 0.75");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.ImageClick);
        _ = restored.Actions[0].ScreenLeft.Should().Be(10);
        _ = restored.Actions[0].ScreenTop.Should().Be(20);
        _ = restored.Actions[0].ScreenWidth.Should().Be(30);
        _ = restored.Actions[0].ScreenHeight.Should().Be(40);
        _ = restored.Actions[0].ImageAssetName.Should().Be("ButtonAsset");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("clicked");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("clickX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("clickY");
        _ = restored.Actions[0].Button.Should().Be(button);
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(1600);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.75);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenWaitImageActionPresent_PreservesStructuredPayload()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.WaitImage,
                ScreenLeft = 1,
                ScreenTop = 2,
                ScreenWidth = 3,
                ScreenHeight = 4,
                ImageAssetName = "DialogAsset",
                ScreenFoundVariableName = "dialogFound",
                ScreenFoundXVariableName = "dialogX",
                ScreenFoundYVariableName = "dialogY",
                ScreenTimeoutMs = 2500,
                ImageSearchSimilarity = 0.625,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Wait Image", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "waitimage 1 2 4 6 DialogAsset dialogFound dialogX dialogY timeout 2500 similarity 0.625");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.WaitImage);
        _ = restored.Actions[0].ScreenLeft.Should().Be(1);
        _ = restored.Actions[0].ScreenTop.Should().Be(2);
        _ = restored.Actions[0].ScreenWidth.Should().Be(3);
        _ = restored.Actions[0].ScreenHeight.Should().Be(4);
        _ = restored.Actions[0].ImageAssetName.Should().Be("DialogAsset");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("dialogFound");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("dialogX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("dialogY");
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(2500);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.625);
    }

    [Theory]
    [InlineData("imagesearch TargetImage similarity NaN")]
    [InlineData("imagesearch TargetImage similarity Infinity")]
    [InlineData("imagesearch TargetImage similarity -Infinity")]
    public void FromMacroSequence_WhenImageSearchSimilarityIsNotFinite_RestoresRawScriptStep(string step)
    {
        var sequence = new MacroSequence { ScriptSteps = { step } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().ContainSingle();
        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[0].Text.Should().Be(step);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenScreenReadingActionsUseVariableTargetColors_PreservesVariableTargetColorMetadata()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 11,
                ScreenY = 22,
                ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable,
                ScreenTargetColorVariableName = "sampled",
                ScreenTimeoutMs = 2500,
                ScreenColorVariableName = "wait_ok",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 3,
                ScreenHeight = 3,
                ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable,
                ScreenTargetColorVariableName = "sampled",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "x",
                ScreenFoundYVariableName = "y",
                ScreenTolerance = 5,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading Variables", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "waitcolor 11 22 $sampled 2500 wait_ok",
            "pixelsearch 0 0 3 3 $sampled found x y timeout 5000 tolerance 5");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().HaveCount(2);

        AssertScreenTargetColor(restored.Actions[0], EditorActionType.WaitColor, "sampled");
        AssertScreenTargetColor(restored.Actions[1], EditorActionType.PixelSearch, "sampled");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingStepUsesDefaults_RestoresStructuredAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps = { "waitcolor 11 22 00FFAA" },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().ContainSingle();
        _ = result.Actions[0].Type.Should().Be(EditorActionType.WaitColor);
        _ = result.Actions[0].ScreenTimeoutMs.Should().Be(EditorActionScreenReadingPayload.DefaultTimeoutMs);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $color == 1c1c1c {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[0].ScriptLeftOperand.Should().Be("color");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        _ = actions[0].ScriptRightOperand.Should().Be("1C1C1C");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesNumericOnlyBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $color == 000000 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(3);
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        _ = actions[0].ScriptRightOperand.Should().Be("000000");
    }
}
