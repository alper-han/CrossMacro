
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptCompilerTests
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptCompiler _compiler;

    public RunScriptCompilerTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = _keyCodeMapper.GetKeyCode("Shift").Returns(42);
        _ = _keyCodeMapper.GetKeyCode("AltGr").Returns(100);
        _ = _keyCodeMapper.GetKeyCode("Enter").Returns(28);
        _ = _keyCodeMapper.GetKeyCode("Tab").Returns(15);
        _ = _keyCodeMapper.GetKeyCodeForCharacter('A').Returns(30);
        _ = _keyCodeMapper.RequiresShift('A').Returns(returnThis: true);
        _ = _keyCodeMapper.RequiresAltGr('A').Returns(returnThis: false);
        _ = _keyCodeMapper.GetKeyCodeForCharacter('@').Returns(16);
        _ = _keyCodeMapper.RequiresShift('@').Returns(returnThis: false);
        _ = _keyCodeMapper.RequiresAltGr('@').Returns(returnThis: true);
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);

        _compiler = new RunScriptCompiler(_keyCodeMapper);
    }

    [Fact]
    public void Compile_WhenTypeStepRequiresShift_EmitsModifierWrappedKeyEvents()
    {
        var result = _compiler.Compile([new RunScriptStep("type A")]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().HaveCount(4);
        _ = result.Sequence.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 42),
            (EventType.KeyPress, 30),
            (EventType.KeyRelease, 30),
            (EventType.KeyRelease, 42));
    }

    [Fact]
    public void Compile_WhenAbsoluteAndRelativeMovesAreMixed_EmitsPerEventCoordinateModes()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("move rel-logical 10 -5"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.IsAbsoluteCoordinates.Should().BeFalse();
        _ = result.Sequence.Events.Should().HaveCount(2);
        _ = result.Sequence.Events.Select(e => (e.Type, e.X, e.Y, e.CoordinateMode)).Should().Equal(
            (EventType.MouseMove, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.MouseMove, 10, -5, MouseCoordinateMode.Relative));
        _ = result.Sequence.Events.Select(e => e.CoordinateSpace).Should().Equal(
            MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateSpace.LogicalDesktop);
        _ = MacroPositionSemantics.GetCoordinateModeSummary(result.Sequence).Should().Be(CoordinateModeSummary.Mixed);
    }

    [Fact]
    public void Compile_WhenLegacyRelativeMoveIsUsed_PreservesRawDeviceSemantics()
    {
        var result = _compiler.Compile([new RunScriptStep("move rel 10 -5")]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().ContainSingle();
        _ = result.Sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = result.Sequence.Events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Theory]
    [InlineData("abs")]
    [InlineData("rel")]
    [InlineData("rel-logical")]
    [InlineData("rel-raw")]
    public void Compile_WhenRuntimeScreenStepFeedsVariableMove_AllowsCoordinateModes(string mode)
    {
        var steps = new[]
        {
            new RunScriptStep("pixelsearch 0 0 10 10 142C2D found found_x found_y"),
            new RunScriptStep($"move {mode} $found_x $found_y"),
        };

        var result = _compiler.Compile(steps);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence.ScriptSteps.Should().Equal(steps.Select(step => step.Step));
    }

    [Fact]
    public void Compile_WhenRuntimeScreenStepFeedsMalformedVariableMove_ReturnsCoordinateDiagnostic()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("pixelsearch 0 0 10 10 142C2D found found_x found_y"),
            new RunScriptStep("move abs $found_x not-a-coordinate"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Invalid move coordinates");
    }

    [Fact]
    public void Compile_WhenRawRelativeMoveThenClick_PropagatesRawDeviceSpace()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move rel-raw 10 -5"),
            new RunScriptStep("click left"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().HaveCount(2);
        _ = result.Sequence.Events.Should().OnlyContain(
            ev => ev.CoordinateMode == MouseCoordinateMode.Relative
                && ev.CoordinateSpace == MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void Compile_WhenRelativeMoveThenClick_EmitsRelativeButtonEventAtCurrentCoordinates()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move rel 10 -5"),
            new RunScriptStep("click left"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().HaveCount(2);
        var click = result.Sequence.Events[1];
        _ = click.Type.Should().Be(EventType.Click);
        _ = click.Button.Should().Be(MacroMouseButton.Left);
        _ = click.UseCurrentPosition.Should().BeFalse();
        _ = click.X.Should().Be(0);
        _ = click.Y.Should().Be(0);
        _ = click.CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = click.CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void Compile_WhenMixedMovesAndClicks_EmitsButtonEventsWithCurrentMoveMode()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("click left"),
            new RunScriptStep("move rel 10 -5"),
            new RunScriptStep("click right"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.IsAbsoluteCoordinates.Should().BeFalse();
        _ = result.Sequence.Events.Should().HaveCount(4);
        _ = result.Sequence.Events.Select(e => (e.Type, e.Button, e.X, e.Y, e.CoordinateMode)).Should().Equal(
            (EventType.MouseMove, MacroMouseButton.None, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.Click, MacroMouseButton.Left, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.MouseMove, MacroMouseButton.None, 10, -5, MouseCoordinateMode.Relative),
            (EventType.Click, MacroMouseButton.Right, 0, 0, MouseCoordinateMode.Relative));
        _ = MacroPositionSemantics.GetCoordinateModeSummary(result.Sequence).Should().Be(CoordinateModeSummary.Mixed);
    }

    [Fact]
    public void Compile_WhenCurrentClickFollowsAbsoluteMove_DoesNotAssignCoordinateMode()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("click current left"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        var click = result.Sequence!.Events[1];
        _ = click.UseCurrentPosition.Should().BeTrue();
        _ = click.X.Should().Be(0);
        _ = click.Y.Should().Be(0);
        _ = click.CoordinateMode.Should().BeNull();
    }

    [Fact]
    public void Compile_WhenMalformedAbsoluteMove_ReturnsFailure()
    {
        var result = _compiler.Compile([new RunScriptStep("move abs 100")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("unsupported step syntax");
    }

    [Fact]
    public void Compile_WhenInvalidStaticCommand_ReturnsPreExtractionDiagnosticWithoutParameterName()
    {
        var result = _compiler.Compile([new RunScriptStep("delay nope")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Invalid delay value. Expected: delay <ms> with ms >= 0.");
        _ = result.ErrorMessage.Should().NotContain("Parameter 'step'");
    }

    [Fact]
    public void Compile_WhenScriptContainsOnlyDelay_PreservesRuntimeDelayStep()
    {
        var result = _compiler.Compile([new RunScriptStep("delay 100")]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().ContainSingle().Which.Should().Be("delay 100");
    }

    [Theory]
    [InlineData("delay nope", "Step 1: Invalid delay value. Expected: delay <ms> with ms >= 0.")]
    [InlineData("move sideways 1 2", "Step 1: Invalid move mode. Expected: abs|absolute|rel|relative|rel-logical|rel-raw.")]
    [InlineData("click invalid", "Step 1: Unknown mouse button 'invalid'.")]
    [InlineData("key press Enter", "Step 1: Invalid key action. Expected: key down <key> | key up <key>.")]
    public void Compile_WhenStaticSyntaxIsInvalid_PreservesExactDiagnosticWithoutParameterSuffix(string step, string expectedError)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(expectedError);
        _ = result.ErrorMessage.Should().NotContain("(Parameter");
    }

    [Fact]
    public void Compile_WhenTypeStepRequiresAltGr_EmitsModifierWrappedKeyEvents()
    {
        var result = _compiler.Compile([new RunScriptStep("type @")]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 100),
            (EventType.KeyPress, 16),
            (EventType.KeyRelease, 16),
            (EventType.KeyRelease, 100));
    }

    [Fact]
    public void Compile_WhenTapContainsSingleModifier_EmitsPressAndRelease()
    {
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);

        var result = _compiler.Compile([new RunScriptStep("tap ctrl")]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 29),
            (EventType.KeyRelease, 29));
    }

    [Fact]
    public void Compile_WhenSetUsesEscapedDollarLiteral_PreservesLiteralTextInCondition()
    {
        var steps = new[]
        {
            new RunScriptStep("set name $$foo"),
            new RunScriptStep("if $name == $$foo {"),
            new RunScriptStep("click current left"),
            new RunScriptStep("}"),
        };

        var result = _compiler.Compile(steps);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().ContainSingle();
        _ = result.Sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = result.Sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
        _ = result.Sequence.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void Compile_WhenColorConditionUsesDifferentHexCasing_ExecutesMatchingBranch()
    {
        var steps = new[]
        {
            new RunScriptStep("set color 1C1C1C"),
            new RunScriptStep("if $color == 1c1c1c {"),
            new RunScriptStep("click current left"),
            new RunScriptStep("}"),
        };

        var result = _compiler.Compile(steps);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().ContainSingle();
        _ = result.Sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = result.Sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
        _ = result.Sequence.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void Compile_WhenTypeCharacterCannotBeMapped_ReturnsDetailedFailure()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter('?').Returns(-1);

        var result = _compiler.Compile([new RunScriptStep("type ?")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("cannot map character '?' for type command");
    }

    [Fact]
    public void Compile_WhenTypeControlKeyCannotBeMapped_PreservesExactDiagnosticWithoutParameterSuffix()
    {
        _ = _keyCodeMapper.GetKeyCode("Enter").Returns(-1);

        var result = _compiler.Compile([new RunScriptStep("type \n")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Unknown key 'Enter'.");
        _ = result.ErrorMessage.Should().NotContain("(Parameter");
    }

    [Fact]
    public void Compile_WhenClipboardGetIsWellFormed_PreservesScriptStep()
    {
        var result = _compiler.Compile([new RunScriptStep("clipboard get $clip")]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().Equal("clipboard get $clip");
    }

    [Theory]
    [InlineData("imageclick Target")]
    [InlineData("imageclick Target clicked click_x click_y")]
    [InlineData("imageclick 0 0 100 100 Target button right similarity 0.9 downsample 2")]
    [InlineData("imageclick 0 0 100 100 Target button left")]
    [InlineData("imageclick 0 0 100 100 Target button middle")]
    [InlineData("imageclick 0 0 100 100 Target clicked click_x click_y button right timeout 5000 similarity 0.9 downsample 2")]
    [InlineData("waitimage Target timeout 5000")]
    [InlineData("waitimage 0 0 100 100 Target found x y timeout 5000 similarity 0.9 downsample 2")]
    public void Compile_WhenImageActionCommandIsWellFormed_PreservesScriptStep(string step)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().ContainSingle().Which.Should().Be(step);
    }

    [Fact]
    public void Compile_WhenClipboardGetHasExtraTokens_ReturnsFailure()
    {
        var result = _compiler.Compile([new RunScriptStep("clipboard get clip extra")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Syntax: clipboard get <var>");
    }

    [Fact]
    public void Compile_WhenShellStepIsWellFormed_PreservesScriptStep()
    {
        var result = _compiler.Compile([new RunScriptStep("shell \"printf hello   world\" 2 100 5000")]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().Equal("shell \"printf hello   world\" 2 100 5000");
    }

    [Fact]
    public void Compile_WhenShellQuotedCommandContainsEscapedBackslashes_PreservesScriptStep()
    {
        const string step = @"shell capture ""printf C:\\temp\\"" code stdout stderr";

        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().Equal(step);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("shell \"\"")]
    public void Compile_WhenShellCommandIsMissing_ReturnsFailure(string step)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Shell command cannot be empty");
    }

    [Theory]
    [InlineData("shell \"echo ok\" -1", "Invalid retries")]
    [InlineData("shell \"echo ok\" 2147483647", "Invalid retries")]
    [InlineData("shell \"echo ok\" 0 -1", "Invalid backoff_ms")]
    [InlineData("shell \"echo ok\" 0 0 -1", "Invalid timeout_ms")]
    [InlineData("shell \"echo ok\" nope", "Invalid retries")]
    [InlineData("shell \"echo ok\" 0 0 0 0", "Syntax: shell")]
    [InlineData("shell \"echo ok\"1", "Syntax: shell")]
    [InlineData("shell capture \"echo ok\" code stdout", "Syntax: shell")]
    [InlineData("shell capture \"echo ok\" 1bad stdout stderr", "Invalid variable name")]
    [InlineData("shell input \"payload\"", "Syntax: shell")]
    [InlineData("shell capture-input \"payload\" \"cat\" code stdout", "Syntax: shell")]
    public void Compile_WhenShellNumericOptionsAreInvalid_ReturnsFailure(string step, string expected)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain(expected);
    }

    [Fact]
    public void Compile_WhenUnquotedShellCommandEndsWithNumericToken_ReturnsAmbiguityFailure()
    {
        var result = _compiler.Compile([new RunScriptStep("shell echo 1")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Quote the shell command");
    }

    [Theory]
    [InlineData("pixelcolor 1 2 #FF0000", "Invalid variable name")]
    [InlineData("waitcolor 1 2 GG0000", "Invalid color")]
    [InlineData("waitcolor 1 2 $1bad 100 wait_ok", "Invalid variable name")]
    [InlineData("pixelcolor 1", "Invalid pixelcolor syntax")]
    [InlineData("pixelcolor one 2 mycolor", "Invalid pixelcolor coordinate")]
    [InlineData("pixelsearch 0 0 ten 10 123456 found_x found_y", "Invalid pixelsearch bounds")]
    [InlineData("pixelsearch 0 0 10 10 123456 found_x found_y tolerance 256", "Invalid pixelsearch tolerance")]
    [InlineData("pixelsearch 0 0 10 10 123456 tolerance -1", "Invalid pixelsearch tolerance")]
    [InlineData("pixelsearch 0 0 10 10 123456 variation 10", "Invalid variable name")]
    [InlineData("imagesearch 10 0 10 10 TargetImage", "Invalid imagesearch bounds")]
    [InlineData("imagesearch TargetImage similarity 1.1", "Invalid imagesearch similarity")]
    [InlineData("imagesearch TargetImage similarity NaN", "Invalid imagesearch similarity")]
    [InlineData("imagesearch TargetImage similarity Infinity", "Invalid imagesearch similarity")]
    [InlineData("imagesearch TargetImage similarity -Infinity", "Invalid imagesearch similarity")]
    [InlineData("imagesearch TargetImage similarity 0,9", "Invalid imagesearch similarity")]
    [InlineData("imagesearch TargetImage downsample 0", "Invalid imagesearch downsample")]
    [InlineData("imagesearch TargetImage matchmode first matchmode best", "Duplicate imagesearch matchmode")]
    [InlineData("imagesearch TargetImage poll poll", "Invalid imagesearch poll")]
    [InlineData("imageclick TargetImage button side1", "Invalid imageclick button")]
    [InlineData("imageclick TargetImage button l", "Invalid imageclick button")]
    [InlineData("imageclick TargetImage button left button right", "Invalid imageclick button")]
    [InlineData("imagesearch TargetImage found 1bad found_y", "Invalid variable name")]
    [InlineData("imagesearch TargetImage similarity 0.9 found found_x found_y", "Unknown imagesearch option")]
    [InlineData("imagesearch $TargetImage", "Invalid image name")]
    [InlineData("pixelcolorful 1 2 sampled", "unsupported step syntax")]
    [InlineData("waitcolorful 1 2 FF0000", "unsupported step syntax")]
    [InlineData("pixelsearchful 0 0 10 10 123456 found_x found_y", "unsupported step syntax")]
    [InlineData("imagesearchful TargetImage", "unsupported step syntax")]
    [InlineData("screenshot", "at least one destination")]
    [InlineData("screenshot region 1 2 0 4 clipboard", "width and height > 0")]
    [InlineData("screenshot region 1 2 3 -4 clipboard", "width and height > 0")]
    [InlineData("screenshot output a.png output b.png", "Duplicate screenshot output")]
    [InlineData("screenshot clipboard clipboard", "Duplicate screenshot clipboard")]
    [InlineData("screenshot region 1 2 3 4 region 5 6 7 8 clipboard", "Unknown screenshot token 'region'")]
    [InlineData("screenshot output", "Syntax: screenshot output <path>")]
    [InlineData("screenshot unknown", "Unknown screenshot token 'unknown'")]
    [InlineData("pixelsearch 0 0 10 10 123456 poll 0", "poll interval")]
    [InlineData("imagesearch TargetImage poll nope", "poll interval")]
    [InlineData("waitcolor 1 2 FF0000 1000 wait_ok poll 0", "poll interval")]
    public void Compile_WhenScreenReadingStepIsMalformed_ReturnsFailure(string step, string expectedError)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData("shell capture \"printf ok\" code stdout stderr")]
    [InlineData("shell capture \"printf ok\" _ stdout _ 1 250 5000")]
    [InlineData("shell input \"payload\" \"cat\" 1 250 5000")]
    [InlineData("shell capture-input \"payload\" \"cat\" code stdout stderr")]
    [InlineData("pixelcolor 1 2")]
    [InlineData("pixelcolor 1 2 mycolor")]
    [InlineData("pixelcolor rel -1 2")]
    [InlineData("pixelcolor rel 1 2 underCursor")]
    [InlineData("waitcolor 1 2 FF0000")]
    [InlineData("waitcolor 1 2 FF0000 1000")]
    [InlineData("waitcolor 1 2 FF0000 1000 wait_ok")]
    [InlineData("waitcolor 1 2 FF0000 1000 wait_ok poll 25")]
    [InlineData("waitcolor 1 2 FF0000 poll 25")]
    [InlineData("waitcolor 1 2 $sampled 100 wait_ok")]
    [InlineData("pixelsearch 0 0 10 10 123456")]
    [InlineData("pixelsearch 0 0 10 10 123456 found_x found_y")]
    [InlineData("pixelsearch 0 0 10 10 123456 found found_x found_y")]
    [InlineData("pixelsearch 0 0 10 10 123456 tolerance 10")]
    [InlineData("pixelsearch 0 0 10 10 123456 found found_x found_y tolerance 26")]
    [InlineData("pixelsearch 0 0 10 10 123456 found found_x found_y timeout 5000 poll 25 tolerance 26")]
    [InlineData("pixelsearch 0 0 10 10 $sampled found found_x found_y tolerance 10")]
    [InlineData("imagesearch TargetImage")]
    [InlineData("imagesearch 0 0 10 10 TargetImage")]
    [InlineData("imagesearch TargetImage found found_x found_y")]
    [InlineData("imagesearch TargetImage similarity 0")]
    [InlineData("imagesearch TargetImage similarity 1")]
    [InlineData("imagesearch TargetImage similarity 0.9")]
    [InlineData("imagesearch TargetImage downsample 2")]
    [InlineData("imagesearch 0 0 10 10 TargetImage found found_x found_y similarity 0.85 downsample 2")]
    [InlineData("imagesearch TargetImage found found_x found_y timeout 5000 poll 25")]
    [InlineData("imageclick TargetImage button right timeout 5000 poll")]
    [InlineData("waitimage TargetImage found found_x found_y timeout 5000 poll 100")]
    [InlineData("screenshot output shot.png")]
    [InlineData("screenshot clipboard")]
    [InlineData("screenshot output shot.png clipboard")]
    [InlineData("screenshot region 1 2 3 4 output shot.png")]
    [InlineData("screenshot region $x $y $w $h clipboard")]
    public void Compile_WhenRuntimeScriptStepIsWellFormed_PreservesScriptStep(string step)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().BeEmpty();
        _ = result.Sequence.ScriptSteps.Should().Equal(step);
    }

    [Fact]
    public void Compile_WhenScreenReadingScriptContainsUnsupportedCommand_ReturnsFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("pixelcolor 1 2 sampled"),
            new RunScriptStep("bogus"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("unsupported step syntax");
    }

    [Fact]
    public void Compile_WhenScreenReadingScriptContainsTopLevelBreak_ReturnsFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("pixelcolor 1 2 sampled"),
            new RunScriptStep("break"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("inside repeat/while/for blocks");
    }

    [Fact]
    public void Compile_WhenScreenReadingScriptUsesRuntimeDelayVariable_PreservesScriptSteps()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set wait_ms 5"),
            new RunScriptStep("pixelcolor 1 2 sampled"),
            new RunScriptStep("delay $wait_ms"),
        ]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.ScriptSteps.Should().Equal("set wait_ms 5", "pixelcolor 1 2 sampled", "delay $wait_ms");
    }

    [Fact]
    public void Compile_WhenScreenReadingScriptUsesMalformedRuntimeDelay_ReturnsFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("pixelcolor 1 2 sampled"),
            new RunScriptStep("delay nope"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("Invalid delay value");
    }

    [Theory]
    [InlineData("mul x 2", 10)]
    [InlineData("div x 2", 2)]
    [InlineData("mul x 1", 5)]
    [InlineData("div x 5", 1)]
    [InlineData("mul x -3", -15)]
    public void Compile_WhenMulDivUsed_ExpandsToExpectedValue(string step, int expectedX)
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep(step),
            new RunScriptStep("move rel $x 0"),
        ]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence.Events.Should().ContainSingle();
        _ = result.Sequence.Events[0].X.Should().Be(expectedX);
    }

    [Fact]
    public void Compile_WhenMulDivUseVariableAmounts_ExpandsToExpectedValue()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep("set factor 4"),
            new RunScriptStep("mul x $factor"),
            new RunScriptStep("set divisor 10"),
            new RunScriptStep("div x $divisor"),
            new RunScriptStep("move rel $x 0"),
        ]);

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence.Events.Should().ContainSingle();
        _ = result.Sequence.Events[0].X.Should().Be(2); // (5 * 4) / 10
    }

    [Fact]
    public void Compile_WhenDivByZero_ReturnsCanonicalFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep("div x 0"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Division by zero is not allowed in mul/div.");
    }

    [Fact]
    public void Compile_WhenDivByZeroThroughVariable_ReturnsCanonicalFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep("set zero 0"),
            new RunScriptStep("div x $zero"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Division by zero is not allowed in mul/div.");
    }

    [Theory]
    [InlineData("mul missing 2")]
    [InlineData("div missing 2")]
    public void Compile_WhenMulDivReferenceUnknownVariable_ReturnsFailure(string step)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: variable 'missing' must exist and be an integer for mul/div.");
    }

    [Fact]
    public void Compile_WhenMulDivTargetIsNotInteger_ReturnsFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set name foo"),
            new RunScriptStep("mul name 2"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: variable 'name' must exist and be an integer for mul/div.");
    }

    [Theory]
    [InlineData("mul x abc", "Step 1: Invalid mul/div amount 'abc'. Expected integer.")]
    [InlineData("div x abc", "Step 1: Invalid mul/div amount 'abc'. Expected integer.")]
    [InlineData("mul x $missing", "Step 1: Unknown variable '$missing'.")]
    public void Compile_WhenMulDivAmountIsInvalid_ReturnsFailure(string step, string expectedError)
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep(step),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("mul x 1 1", "mul")]
    [InlineData("div x 1 1", "div")]
    public void Compile_WhenMulDivSyntaxIsInvalid_ReturnsFailure(string step, string command)
    {
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be($"Step 1: Invalid {command} syntax. Expected: {command} <name> [amount].");
    }

    [Theory]
    [InlineData("mul")]
    [InlineData("div")]
    public void Compile_WhenMulDivBare_FallsThroughToUnsupportedStep(string step)
    {
        // Mirrors bare inc/dec: "<cmd>" without a payload is not a variable command.
        var result = _compiler.Compile([new RunScriptStep(step)]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be($"Step 1: unsupported step syntax '{step}'.");
    }

    [Fact]
    public void Compile_WhenMulDivVariableNameIsInvalid_ReturnsFailure()
    {
        var result = _compiler.Compile([new RunScriptStep("mul 1x 2")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*");
    }

    [Fact]
    public void Compile_WhenMulOverflowsIntRange_ReturnsOutOfRangeFailure()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 2000000000"),
            new RunScriptStep("mul x 3"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Result is out of range for mul/div.");
    }

    [Fact]
    public void Compile_WhenDivHitsIntMinOverMinusOne_ReturnsOutOfRangeInsteadOfThrowing()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x -2147483648"),
            new RunScriptStep("div x -1"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: Result is out of range for mul/div.");
    }

    [Fact]
    public void Compile_WhenRepeatCountIsSpacelessArithmetic_ExpandsEvaluatedCount()
    {
        // Regression pin for the closed divergence window: the runtime executor used to
        // evaluate `repeat 5+3 {` while compile-time expansion rejected the header.
        var result = _compiler.Compile(
        [
            new RunScriptStep("repeat 5+3 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().HaveCount(8);
        _ = result.Sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
    }

    [Fact]
    public void Compile_WhenRepeatCountIsSpacedArithmetic_ExpandsEvaluatedCount()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set a 10"),
            new RunScriptStep("repeat $a / 2 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().HaveCount(5);
    }

    [Fact]
    public void Compile_WhenRepeatCountIsMalformedExpression_FailsWithRepeatCountLabel()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("repeat $a / {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: '$a /' is not a valid numeric expression for repeat count.");
    }

    [Fact]
    public void Compile_WhenRepeatCountEvaluatesNegative_KeepsLegacyWording()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("repeat 2 - 5 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: repeat count must be >= 0.");
    }

    [Fact]
    public void Compile_WhenForSegmentsAreArithmetic_ExpandsEvaluatedRange()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set n 3"),
            new RunScriptStep("for i from $n - 2 to $n * 2 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        // i runs 1..6 inclusive: six clicks.
        _ = result.Sequence!.Events.Should().HaveCount(6);
    }

    [Fact]
    public void Compile_WhenForStepIsArithmeticZero_KeepsLegacyZeroStepWording()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set a 1"),
            new RunScriptStep("for i from $a to 3 step $a - 1 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: for step cannot be 0.");
    }

    [Fact]
    public void Compile_WhenConditionOperandIsArithmetic_EvaluatesNumericComparison()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("set x 5"),
            new RunScriptStep("if $x + 1 > 5 {"),
            new RunScriptStep("click left"),
            new RunScriptStep("}"),
        ]);

        _ = result.Success.Should().BeTrue();
        _ = result.Sequence.Should().NotBeNull();
        _ = result.Sequence!.Events.Should().ContainSingle(ev => ev.Type == EventType.Click);
    }
}
