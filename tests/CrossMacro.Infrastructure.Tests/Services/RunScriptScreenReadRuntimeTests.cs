using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptScreenReadRuntimeTests
{
    [Fact]
    public async Task PlayAsync_WhenScreenReadingScriptSteps_SamplesAndStoresRuntimeVariables()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
            RelativePixelColor = new ScreenPixelColor(0xAA, 0xBB, 0xCC),
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33))
        };
        var positionProvider = CreatePositionProvider((50, 60));
        using var player = CreatePlayer(positionProvider, screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 10 20 sampled",
                "pixelcolor rel 5 -3 relativeSampled",
                "waitcolor 1 2 00FF00 123",
                "pixelsearch 0 0 10 12 112233 found_x found_y tolerance 10"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.GetPixelPoints.Should().Equal(new ScreenPoint(10, 20), new ScreenPoint(55, 57));
        screenReader.WaitCalls.Should().ContainSingle(call =>
            call.Point == new ScreenPoint(1, 2)
            && call.Expected == new ScreenPixelColor(0x00, 0xFF, 0x00)
            && call.Options.Timeout == TimeSpan.FromMilliseconds(123));
        screenReader.SearchCalls.Should().ContainSingle(call =>
            call.Region.X == 0
            && call.Region.Y == 0
            && call.Region.Width == 10
            && call.Region.Height == 12
            && call.Expected == new ScreenPixelColor(0x11, 0x22, 0x33)
            && call.Tolerance == 10);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("sampled", "123456");
        variables.Should().Contain("relativeSampled", "AABBCC");
        variables.Should().Contain("found_x", "7");
        variables.Should().Contain("found_y", "8");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorUsesVariableTargetColor_PassesSampledColorToScreenReader()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56)
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "waitcolor 3 4 $sampled 100 wait_ok"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.WaitCalls.Should().ContainSingle(call =>
            call.Point == new ScreenPoint(3, 4)
            && call.Expected == new ScreenPixelColor(0x12, 0x34, 0x56)
            && call.Options.Timeout == TimeSpan.FromMilliseconds(100));

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("sampled", "123456");
        variables.Should().Contain("wait_ok", "true");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchUsesVariableTargetColor_PassesSampledColorToScreenReader()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33))
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "pixelsearch 0 0 10 12 $sampled found found_x found_y tolerance 10"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.SearchCalls.Should().ContainSingle(call =>
            call.Region == new ScreenRect(0, 0, 10, 12)
            && call.Expected == new ScreenPixelColor(0x12, 0x34, 0x56)
            && call.Tolerance == 10);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("sampled", "123456");
        variables.Should().Contain("found", "true");
        variables.Should().Contain("found_x", "7");
        variables.Should().Contain("found_y", "8");
    }

    [Fact]
    public async Task PlayAsync_WhenScreenReadingOnlyScriptLoops_RepeatsScriptSteps()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56)
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelcolor 10 20 sampled"]
        };

        await player.PlayAsync(macro, new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 3
        }, CancellationToken.None);

        screenReader.GetPixelPoints.Should().Equal(
            new ScreenPoint(10, 20),
            new ScreenPoint(10, 20),
            new ScreenPoint(10, 20));
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeOnlyClipboardDelayAndVariables_DoesNotAcquireSimulator()
    {
        var timingService = new RecordingTimingService();
        var clipboard = Substitute.For<IClipboardService>();
        clipboard.IsSupported.Returns(true);
        clipboard.GetTextAsync(Arg.Any<CancellationToken>()).Returns("clipboard text");
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new FakeScreenPixelReader(),
            timingService: timingService,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"),
            clipboardService: clipboard);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "clipboard set \"hello   spaced   world\"",
                "delay 5",
                "clipboard get $clip",
                "set copied=$clip",
                "clipboard set $copied"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("hello   spaced   world", Arg.Any<CancellationToken>());
        await clipboard.Received(1).SetTextAsync("clipboard text", Arg.Any<CancellationToken>());
        timingService.WaitCalls.Should().ContainSingle().Which.Should().Be(5);
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeOnlyControlFlow_DoesNotAcquireSimulator()
    {
        var clipboard = Substitute.For<IClipboardService>();
        clipboard.IsSupported.Returns(true);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new FakeScreenPixelReader(),
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"),
            clipboardService: clipboard);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "set n=1",
                "if $n == 1 {",
                "clipboard set ok",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("ok", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayAsync_WhenMixedScreenReadingAndInputSteps_PreservesOriginalOrder()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);

        var compiler = new RunScriptCompiler(Substitute.For<IKeyCodeMapper>());
        var compileResult = compiler.Compile(
            [
                new RunScriptStep("move rel 10 20"),
                new RunScriptStep("pixelcolor 1 2 sampled"),
                new RunScriptStep("click left"),
                new RunScriptStep("waitcolor 3 4 FFFFFF 10")
            ]);

        compileResult.Success.Should().BeTrue(compileResult.ErrorMessage);

        await player.PlayAsync(compileResult.Sequence!, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "input:move:10,20",
            "screen:pixelcolor:1,2",
            "input:click:left",
            "screen:waitcolor:3,4");
    }

    [Fact]
    public async Task PlayAsync_WhenMixedScreenReadingAndDelaySteps_ExecutesDelayAndContinues()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);

        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "delay 1",
                "click left"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesMoveAbsoluteAlias_InitializesSimulatorWithResolution()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);

        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "move absolute 100 200"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        inputSimulator.InitializedWidth.Should().Be(1920);
        inputSimulator.InitializedHeight.Should().Be(1080);
        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:move-abs:100,200");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelColorFeedsIfCondition_ExecutesMatchingRuntimeBranch()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "if $sampled == 123456 {",
                "click left",
                "}",
                "else {",
                "move rel 9 9",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelColorFeedsLowercaseHexWhileCondition_ExecutesMatchingRuntimeBranch()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            PixelColor = new ScreenPixelColor(0x1C, 0x1C, 0x1C)
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "while $sampled == 1c1c1c {",
                "click left",
                "break",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeVariablesUseSetIncDec_PreservesCaseInsensitiveDictionaryAndValues()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "set Count=1",
                "inc count 2",
                "set Amount 4",
                "dec COUNT $Amount",
                "set combined $count",
                "if $COUNT == -1 {",
                "click left",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables["COUNT"].Should().Be("-1");
        variables["combined"].Should().Be("-1");
        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesRepeatForBreakContinue_PreservesPlaybackOrdering()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "repeat 2 {",
                "for i from 1 to 3 {",
                "if $i == 2 {",
                "continue",
                "}",
                "if $i == 3 {",
                "break",
                "}",
                "click left",
                "}",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRepeatContainsNestedIfElse_ExecutesPostElseBodyInsideLoop()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "repeat 2 {",
                "if $sampled == FFFFFF {",
                "move rel 1 1",
                "}",
                "else {",
                "move rel 2 2",
                "}",
                "click left",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:move:2,2",
            "input:click:left",
            "input:move:2,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeDelayUsesVariable_PreservesScaledTiming()
    {
        var activity = new List<string>();
        var timingService = new RecordingTimingService();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator, timingService);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "set wait_ms 25",
                "delay $wait_ms",
                "click left"
            ]
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 2.5 }, CancellationToken.None);

        timingService.WaitCalls.Should().Equal(10);
        activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeConditionReferencesMissingVariable_ThrowsExactMessage()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "if $missing == 1 {",
                "click left",
                "}"
            ]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unknown variable '$missing'.");
        activity.Should().Equal("screen:pixelcolor:1,2");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeForStepIsZero_ThrowsExactMessage()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 1 2 sampled",
                "for i from 1 to 3 step 0 {",
                "click left",
                "}"
            ]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("For step cannot be 0.");
        activity.Should().Equal("screen:pixelcolor:1,2");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorHasResultVariable_StoresFalseAndContinuesOnTimeout()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                "waitcolor timed out")
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "waitcolor 3 4 FFFFFF 10 wait_ok",
                "if $wait_ok == false {",
                "click left",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("wait_ok", "false");
        activity.Should().Equal(
            "screen:waitcolor:3,4",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorUsesMissingColorVariable_ThrowsVariableResolutionMessage()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["waitcolor 3 4 $sampled 100 wait_ok"]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: color variable 'sampled' is not defined.");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorUsesMalformedColorVariableValue_ThrowsValueSpecificMessage()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "set sampled not-a-color",
                "waitcolor 3 4 $sampled 100 wait_ok"
            ]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 2: color variable 'sampled' value 'not-a-color' is invalid. Expected RRGGBB.");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasFoundVariable_StoresFalseCoordinatesAndContinuesOnNoMatch()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            SearchResult = ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                "pixelsearch found no matching pixel")
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "if $found == false {",
                "click left",
                "}"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("found", "false");
        variables.Should().Contain("found_x", "-1");
        variables.Should().Contain("found_y", "-1");
        activity.Should().Equal(
            "screen:pixelsearch:0,0",
            "input:click:left");
    }

    [Theory]
    [InlineData("pixelsearch 0 0 10 12 112233 found_x found_y", null, "found_x", "found_y", 0)]
    [InlineData("pixelsearch 0 0 10 12 112233 found found_x found_y", "found", "found_x", "found_y", 0)]
    [InlineData("pixelsearch 0 0 10 12 112233 tolerance 7", null, null, null, 7)]
    [InlineData("pixelsearch 0 0 10 12 112233 found_x found_y tolerance 10", null, "found_x", "found_y", 10)]
    [InlineData("pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 26", "found", "found_x", "found_y", 26)]
    public async Task PlayAsync_WhenPixelSearchUsesSupportedLayouts_AppliesToleranceAndStoresVariables(
        string step,
        string? foundVariable,
        string? xVariable,
        string? yVariable,
        int expectedTolerance)
    {
        var screenReader = new FakeScreenPixelReader
        {
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33))
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = [step]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.SearchCalls.Should().ContainSingle(call => call.Tolerance == expectedTolerance);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        if (foundVariable != null)
        {
            variables.Should().Contain(foundVariable, "true");
        }

        if (xVariable != null && yVariable != null)
        {
            variables.Should().Contain(xVariable, "7");
            variables.Should().Contain(yVariable, "8");
        }
        else
        {
            variables.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorTimesOut_ThrowsRuntimeFailure()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                "waitcolor timed out")
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["waitcolor 1 2 FFFFFF 10"]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*waitcolor failed: CaptureTimeout: waitcolor timed out*");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorReturnsCanceled_StopsCleanly()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled")
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["waitcolor 1 2 FFFFFF 10"]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.WaitCalls.Should().ContainSingle();
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorHasResultVariable_ReturnsCanceled_StopsCleanly()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled")
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "waitcolor 1 2 FFFFFF 10 wait_ok",
                "click left"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal("screen:waitcolor:1,2");
        ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorReturnsCanceled_DoesNotRunLaterScreenReads()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled")
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "waitcolor 1 2 FFFFFF 10",
                "pixelsearch 0 0 1 1 FFFFFF found_x found_y"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.WaitCalls.Should().ContainSingle();
        screenReader.SearchCalls.Should().BeEmpty();
        ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasFoundVariable_ReturnsCanceled_StopsCleanly()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            SearchResult = ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.Canceled,
                "pixelsearch canceled")
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "click left"
            ]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal("screen:pixelsearch:0,0");
        ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task WaitForPixelAsync_WhenExpectedColorSeen_DisposesEachCapturedFrameOnce()
    {
        var provider = new DisposalTrackingFrameProvider(
            new ScreenPixelColor(0x00, 0x00, 0x00),
            new ScreenPixelColor(0x00, 0xFF, 0x00));
        using var reader = new ScreenPixelReader(provider);

        var result = await reader.WaitForPixelAsync(
            new ScreenPoint(1, 2),
            new ScreenPixelColor(0x00, 0xFF, 0x00),
            new ScreenReadOptions(timeout: TimeSpan.FromSeconds(1), pollInterval: TimeSpan.Zero));

        result.IsSuccess.Should().BeTrue();
        provider.CaptureCalls.Should().Be(2);
        provider.Owners.Should().AllSatisfy(owner => owner.DisposeCount.Should().Be(1));
    }

    [Fact]
    public async Task WaitForPixelAsync_WhenTimeoutExpires_DisposesCapturedFrameOnce()
    {
        var provider = new DisposalTrackingFrameProvider(new ScreenPixelColor(0x00, 0x00, 0x00));
        using var reader = new ScreenPixelReader(provider);

        var result = await reader.WaitForPixelAsync(
            new ScreenPoint(1, 2),
            new ScreenPixelColor(0x00, 0xFF, 0x00),
            new ScreenReadOptions(timeout: TimeSpan.Zero, pollInterval: TimeSpan.Zero));

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
        provider.CaptureCalls.Should().Be(1);
        provider.Owners.Should().ContainSingle(owner => owner.DisposeCount == 1);
    }

    [Fact]
    public async Task WaitForPixelAsync_WhenCanceledAfterCapture_DisposesCapturedFrameAndReturnsCanceled()
    {
        using var cts = new CancellationTokenSource();
        var provider = new DisposalTrackingFrameProvider(new ScreenPixelColor(0x00, 0x00, 0x00))
        {
            AfterCapture = () => cts.Cancel()
        };
        using var reader = new ScreenPixelReader(provider);

        var result = await reader.WaitForPixelAsync(
            new ScreenPoint(1, 2),
            new ScreenPixelColor(0x00, 0xFF, 0x00),
            new ScreenReadOptions(
                timeout: TimeSpan.FromMinutes(1),
                pollInterval: TimeSpan.FromMinutes(1),
                cancellationToken: cts.Token));

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.Canceled);
        provider.CaptureCalls.Should().Be(1);
        provider.Owners.Should().ContainSingle(owner => owner.DisposeCount == 1);
    }

    [Fact]
    public async Task SearchPixelAsync_WhenCapturedRegionHasOnlyInvalidMaskedPixels_ReturnsOutOfBounds()
    {
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 1),
            [[Black, Black]],
            validPixelMask: [0, 0]);
        using var reader = new ScreenPixelReader(new SingleFrameProvider(frame));

        var result = await reader.SearchPixelAsync(
            new ScreenRect(0, 0, 2, 1),
            Black,
            tolerance: 0,
            ScreenReadOptions.Default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.OutOfBounds);
    }

    [Fact]
    public async Task SearchImageAsync_WhenCapturedRegionHasOnlyInvalidMaskedPixels_ReturnsOutOfBounds()
    {
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 1),
            [[Black, Black]],
            validPixelMask: [0, 0]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new SingleFrameProvider(frame));

        var result = await reader.SearchImageAsync(
            new ScreenRect(0, 0, 2, 1),
            template,
            ScreenImageMatchOptions.Default,
            ScreenReadOptions.Default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.OutOfBounds);
    }

    [Fact]
    public async Task SearchImageAsync_WhenCanceledBeforeCapture_ReturnsCanceled()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new SingleFrameProvider(frame));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await reader.SearchImageAsync(
            null,
            template,
            ScreenImageMatchOptions.Default,
            new ScreenReadOptions(cancellationToken: cts.Token));

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.Canceled);
    }

    [Fact]
    public async Task SearchImageAsync_WhenMatchExceedsTimeout_ReturnsCaptureTimeout()
    {
        using var frame = CreateSolidRgbFrame(new ScreenRect(0, 0, 1024, 128), Black);
        using var template = CreateSolidRgbFrame(new ScreenRect(0, 0, 32, 8), Black);
        using var reader = new ScreenPixelReader(new IgnoringCancellationFrameProvider(frame));

        var result = await reader.SearchImageAsync(
            null,
            template,
            ScreenImageMatchOptions.Create(null, 1.0, 1, ScreenImageMatchSelectionMode.BestMatch),
            new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
        result.ErrorMessage.Should().Contain("Timed out");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasNoMatch_ThrowsRuntimeFailure()
    {
        var screenReader = new FakeScreenPixelReader
        {
            SearchResult = ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                "pixelsearch found no matching pixel")
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelsearch 0 0 1 1 FFFFFF found_x found_y"]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pixelsearch failed: CaptureTimeout: pixelsearch found no matching pixel*");

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().NotContainKey("found_x");
        variables.Should().NotContainKey("found_y");
    }

    [Theory]
    [InlineData(ScreenReadErrorKind.Unsupported, "waitcolor unsupported")]
    [InlineData(ScreenReadErrorKind.PermissionDenied, "waitcolor permission denied")]
    [InlineData(ScreenReadErrorKind.OutOfBounds, "waitcolor out of bounds")]
    [InlineData(ScreenReadErrorKind.BackendUnavailable, "waitcolor backend unavailable")]
    [InlineData(ScreenReadErrorKind.CaptureFailed, "waitcolor capture failed")]
    public async Task PlayAsync_WhenWaitColorHasResultVariable_InfrastructureFailuresThrowAndStop(
        ScreenReadErrorKind errorKind,
        string errorMessage)
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            WaitResult = ScreenReadResult<ScreenPixelColor>.Failure(errorKind, errorMessage)
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "waitcolor 3 4 FFFFFF 10 wait_ok",
                "click left"
            ]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*waitcolor failed: {errorKind}: {errorMessage}*");

        activity.Should().Equal("screen:waitcolor:3,4");
        ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ScreenReadErrorKind.Unsupported, "pixelsearch unsupported")]
    [InlineData(ScreenReadErrorKind.PermissionDenied, "pixelsearch permission denied")]
    [InlineData(ScreenReadErrorKind.OutOfBounds, "pixelsearch out of bounds")]
    [InlineData(ScreenReadErrorKind.BackendUnavailable, "pixelsearch backend unavailable")]
    [InlineData(ScreenReadErrorKind.CaptureFailed, "pixelsearch capture failed")]
    public async Task PlayAsync_WhenPixelSearchHasFoundVariable_InfrastructureFailuresThrowAndStop(
        ScreenReadErrorKind errorKind,
        string errorMessage)
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            SearchResult = ScreenReadResult<ScreenPixelSearchMatch>.Failure(errorKind, errorMessage)
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "click left"
            ]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*pixelsearch failed: {errorKind}: {errorMessage}*");

        activity.Should().Equal("screen:pixelsearch:0,0");
        ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenPixelColorUsesLowerValue_StoresCanonicalUppercaseRgb()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0xAB, 0xCD, 0xEF)
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelcolor 10 20 color"]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("color", "ABCDEF");
    }

    [Fact]
    public async Task PlayAsync_WhenCancellationRequestedBeforeScreenRead_PropagatesCancellation()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var macro = new MacroSequence
        {
            ScriptSteps = ["pixelcolor 1 2 sampled"]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        screenReader.GetPixelPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitImageTimeoutExpires_ForwardsRemainingDeadlineToImageSearch()
    {
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var screenReader = new FakeScreenPixelReader
        {
            ImageSearchResult = ScreenReadResult<ScreenImageMatch>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                "No image matching the template was found.")
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["waitimage Target found found_x found_y timeout 0"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.LastImageReadOptions.Timeout.Should().Be(TimeSpan.Zero);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("found", "false");
        variables.Should().Contain("found_x", "-1");
        variables.Should().Contain("found_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeVariableOnlyScriptHasNoEvents_RunsWithoutSimulator()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            screenReader,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"));
        var macro = new MacroSequence
        {
            ScriptSteps = ["set c=123456"]
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("c", "123456");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchFindsTemplate_StoresFoundFlagAndCoordinates()
    {
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 3),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black]
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White]
            ]);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(frame)));
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch 0 0 4 3 Target found found_x found_y similarity 1 downsample 1"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("found", "true");
        variables.Should().Contain("found_x", "1");
        variables.Should().Contain("found_y", "1");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchUsesBestMode_ForwardsSelectionMode()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch Target found found_x found_y matchmode best"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        screenReader.LastImageOptions.SelectionMode.Should().Be(ScreenImageMatchSelectionMode.BestMatch);
    }

    [Fact]
    public async Task SearchImageAsync_WhenFirstModeFindsImmediateMatchAfterTimeout_ReturnsSuccess()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new DelayedFrameProvider(frame));

        var result = await reader.SearchImageAsync(
            null,
            template,
            ScreenImageMatchOptions.Create(null, 1.0, 1, ScreenImageMatchSelectionMode.FirstThresholdMatch),
            new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        result.IsSuccess.Should().BeTrue($"{result.ErrorKind}: {result.ErrorMessage}");
        result.Value.Point.Should().Be(new ScreenPoint(0, 0));
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenImageSearchHasUnknownOption_RejectsTheStep()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var executor = new RunScriptScreenReadExecutor(new ScreenPixelReader(new SingleFrameProvider(frame)), null);

        var act = async () => await executor.ExecuteStepAsync(
            "imagesearch Target found found_x found_y unsupported 1",
            3,
            new Dictionary<string, string>(),
            CancellationToken.None,
            new Dictionary<string, string> { ["Target"] = EncodePngBase64(template) });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 3: imagesearch failed: Invalid imagesearch syntax.*");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchDoesNotFindTemplate_StoresFalseAndNegativeCoordinates()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 2, 2), Solid(2, 2, Black));
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(frame)));
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch MissingPixel found found_x found_y"],
            Images = { ["MissingPixel"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("found", "false");
        variables.Should().Contain("found_x", "-1");
        variables.Should().Contain("found_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchImageAssetIsMissing_ThrowsStepNumberedFailure()
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch Target found found_x found_y"]
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: image asset 'Target' is not defined.");
    }

	[Theory]
	[InlineData("not-base64", "not valid Base64")]
	[InlineData("bm90IGEgcG5n", "not a supported PNG")]
    public async Task PlayAsync_WhenImageSearchImageAssetIsInvalid_ThrowsStepNumberedFailure(string asset, string expectedMessage)
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch Target found found_x found_y"],
            Images = { ["Target"] = asset }
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Step 1: imagesearch failed: image asset 'Target' is {expectedMessage}*");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchImageAssetExceedsSupportedDimensions_ThrowsStepNumberedFailure()
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = ["imagesearch Target found found_x found_y"],
            Images = { ["Target"] = Convert.ToBase64String(CreateOversizedPngBytes()) }
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: image asset 'Target' is not a supported PNG: *maximum supported size of 7680x4320*");
    }

	[Theory]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("-Infinity")]
	public async Task ExecuteStepAsync_WhenImageSearchSimilarityIsNotFinite_ThrowsStepNumberedFailure(string similarity)
	{
		using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
		using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
		var executor = new RunScriptScreenReadExecutor(new ScreenPixelReader(new SingleFrameProvider(frame)), null);
		var runtimeVariables = new Dictionary<string, string>();
		var step = $"imagesearch Target found found_x found_y similarity {similarity}";

		var act = async () => await executor.ExecuteStepAsync(
			step,
			1,
			runtimeVariables,
			CancellationToken.None,
			new Dictionary<string, string> { ["Target"] = EncodePngBase64(template) });

		await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: Invalid imagesearch similarity. Expected number between 0.0 and 1.0.");
	}

	[Fact]
    public async Task PlayAsync_WhenImageClickFindsTemplate_ClicksTemplateCenter()
	{
        var activity = new List<string>();
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 3),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black]
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White]
            ]);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = ["imageclick Target clicked click_x click_y button right similarity 1 downsample 1"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal("input:move-abs:2,2", "input:click:right");
        inputSimulator.InitializedWidth.Should().Be(1920);
        inputSimulator.InitializedHeight.Should().Be(1080);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("clicked", "true");
        variables.Should().Contain("click_x", "2");
         variables.Should().Contain("click_y", "2");
     }

    [Fact]
    public async Task PlayAsync_WhenImageClickLacksAbsoluteCoordinates_UsesRelativeMovementAndPreservesSignedVariables()
    {
        var activity = new List<string>();
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 4),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black],
                [Black, Black, Black, Black]
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White]
            ]);
        var inputSimulator = new RecordingInputSimulator(activity) { SupportsAbsoluteCoordinates = false };
        using var player = CreatePlayer(
            CreatePositionProvider((10, 20)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = ["imageclick Target clicked click_x click_y"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().Equal("input:move:-8,-18", "input:click:left");
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("clicked", "true");
        variables.Should().Contain("click_x", "2");
        variables.Should().Contain("click_y", "2");
    }

    [Fact]
    public async Task PlayAsync_WhenImageClickDoesNotFindTemplateAndHasVariables_StoresFalseAndDoesNotClick()
    {
        var activity = new List<string>();
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 2, 2), Solid(2, 2, Black));
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = ["imageclick MissingPixel clicked click_x click_y"],
            Images = { ["MissingPixel"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        activity.Should().BeEmpty();
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("clicked", "false");
        variables.Should().Contain("click_x", "-1");
        variables.Should().Contain("click_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitImageFindsTemplateAfterPolling_StoresFoundFlagAndCoordinates()
    {
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        using var provider = new DisposalTrackingFrameProvider(Black, Red);
        using var reader = new ScreenPixelReader(provider);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), reader);
        var macro = new MacroSequence
        {
            ScriptSteps = ["waitimage Target found found_x found_y timeout 1000 similarity 1"],
            Images = { ["Target"] = EncodePngBase64(template) }
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        provider.CaptureCalls.Should().Be(2);
        reader.TemplateNormalizationCount.Should().Be(1);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        variables.Should().Contain("found", "true");
        variables.Should().Contain("found_x", "1");
        variables.Should().Contain("found_y", "2");
    }

    private static MacroPlayer CreatePlayer(
        IMousePositionProvider positionProvider,
        IScreenPixelReader screenReader,
        IInputSimulator? inputSimulator = null,
        IPlaybackTimingService? timingService = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IClipboardService? clipboardService = null)
    {
        var keyCodeMapper = CreateKeyCodeMapper();
        return new MacroPlayer(
            positionProvider,
            new PlaybackValidator(keyCodeMapper, positionProvider),
            timingService: timingService,
            playbackWaitAsync: (_, _) => Task.CompletedTask,
            inputSimulatorFactory: inputSimulatorFactory ?? (() => inputSimulator ?? Substitute.For<IInputSimulator>()),
            screenPixelReader: screenReader,
            keyCodeMapper: keyCodeMapper,
            clipboardService: clipboardService);
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(false);
        keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }

    private static IMousePositionProvider CreatePositionProvider((int X, int Y) position)
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.ProviderName.Returns("fake-position");
        positionProvider.IsSupported.Returns(true);
        positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(position));
        positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        return positionProvider;
    }

    private static readonly ScreenPixelColor Black = new(0x00, 0x00, 0x00);
    private static readonly ScreenPixelColor Red = new(0xFF, 0x00, 0x00);
    private static readonly ScreenPixelColor Green = new(0x00, 0xFF, 0x00);
    private static readonly ScreenPixelColor Blue = new(0x00, 0x00, 0xFF);
    private static readonly ScreenPixelColor White = new(0xFF, 0xFF, 0xFF);

    private static ScreenPixelColor[][] Solid(int width, int height, ScreenPixelColor color)
    {
        var rows = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            rows[y] = new ScreenPixelColor[width];
            Array.Fill(rows[y], color);
        }

        return rows;
    }

    private static ScreenFrame CreateRgbFrame(ScreenRect bounds, ScreenPixelColor[][] pixels, byte[]? validPixelMask = null)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = y * stride + x * 3;
                bytes[offset] = pixels[y][x].R;
                bytes[offset + 1] = pixels[y][x].G;
                bytes[offset + 2] = pixels[y][x].B;
            }
        }

        var mask = validPixelMask is null ? ReadOnlyMemory<byte>.Empty : validPixelMask;
        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, bytes, validPixelMask: mask);
    }

    private static ScreenFrame CreateSolidRgbFrame(ScreenRect bounds, ScreenPixelColor color)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = y * stride + x * 3;
                bytes[offset] = color.R;
                bytes[offset + 1] = color.G;
                bytes[offset + 2] = color.B;
            }
        }

        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, bytes);
    }

    private static string EncodePngBase64(ScreenFrame frame)
    {
        using var stream = new MemoryStream();
        ScreenFramePngEncoder.Encode(frame, stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x6C, 0xF7, 0xBC, 0x13
        ];
    }

    private sealed class FakeScreenPixelReader : IScreenPixelReader, IScreenImageSearchReader
    {
        private int _getPixelCallCount;

        public string ProviderName => "fake-screen-reader";

        public bool IsSupported => true;

        public ScreenPixelColor PixelColor { get; init; } = new(0x00, 0x00, 0x00);

        public ScreenPixelColor RelativePixelColor { get; init; } = new(0x00, 0x00, 0x00);

        public ScreenPixelSearchMatch SearchMatch { get; init; } = new(new ScreenPoint(0, 0), new ScreenPixelColor(0x00, 0x00, 0x00));

        public ScreenReadResult<ScreenPixelColor>? WaitResult { get; init; }

        public ScreenReadResult<ScreenPixelSearchMatch>? SearchResult { get; init; }

        public ScreenReadResult<ScreenImageMatch>? ImageSearchResult { get; init; }

        public ScreenReadOptions LastImageReadOptions { get; private set; }

        public ScreenImageMatchOptions LastImageOptions { get; private set; } = ScreenImageMatchOptions.Default;

        public List<ScreenPoint> GetPixelPoints { get; } = [];

        public List<(ScreenPoint Point, ScreenPixelColor Expected, ScreenReadOptions Options)> WaitCalls { get; } = [];

        public List<(ScreenRect Region, ScreenPixelColor Expected, int Tolerance, ScreenReadOptions Options)> SearchCalls { get; } = [];

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            GetPixelPoints.Add(point);
            var color = _getPixelCallCount++ == 0 ? PixelColor : RelativePixelColor;
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(color));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            WaitCalls.Add((point, expected, options));
            return Task.FromResult(WaitResult ?? ScreenReadResult<ScreenPixelColor>.Success(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            SearchCalls.Add((region, expected, tolerance, options));
            return Task.FromResult(SearchResult ?? ScreenReadResult<ScreenPixelSearchMatch>.Success(SearchMatch));
        }

        public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
            ScreenRect? region,
            ScreenFrame template,
            ScreenImageMatchOptions options,
            ScreenReadOptions readOptions)
        {
            readOptions.CancellationToken.ThrowIfCancellationRequested();
            LastImageOptions = options;
            LastImageReadOptions = readOptions;
            return Task.FromResult(ImageSearchResult ?? ScreenReadResult<ScreenImageMatch>.Success(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class SingleFrameProvider : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame;

        public SingleFrameProvider(ScreenFrame frame)
        {
            _frame = frame;
        }

        public string ProviderName => "single-frame-provider";

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(_frame));
        }

        public void Dispose()
        {
        }
    }

    private sealed class IgnoringCancellationFrameProvider : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame;

        public IgnoringCancellationFrameProvider(ScreenFrame frame)
        {
            _frame = frame;
        }

        public string ProviderName => "ignoring-cancellation-frame-provider";

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options) =>
            Task.FromResult(ScreenReadResult<ScreenFrame>.Success(_frame));

        public void Dispose()
        {
        }
    }

    private sealed class DelayedFrameProvider : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame;

        public DelayedFrameProvider(ScreenFrame frame)
        {
            _frame = frame;
        }

        public string ProviderName => "delayed-frame-provider";

        public bool IsSupported => true;

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return ScreenReadResult<ScreenFrame>.Success(_frame);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingScreenPixelReader : IScreenPixelReader
    {
        private readonly List<string> _activity;

        public RecordingScreenPixelReader(List<string> activity)
        {
            _activity = activity;
        }

        public string ProviderName => "recording-screen-reader";

        public bool IsSupported => true;

        public ScreenPixelColor PixelColor { get; init; } = new(0x12, 0x34, 0x56);

        public ScreenReadResult<ScreenPixelColor>? WaitResult { get; init; }

        public ScreenReadResult<ScreenPixelSearchMatch>? SearchResult { get; init; }

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:pixelcolor:{point.X},{point.Y}");
            return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(PixelColor));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:waitcolor:{point.X},{point.Y}");
            return Task.FromResult(WaitResult ?? ScreenReadResult<ScreenPixelColor>.Success(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:pixelsearch:{region.X},{region.Y}");
            return Task.FromResult(SearchResult ?? ScreenReadResult<ScreenPixelSearchMatch>.Success(new ScreenPixelSearchMatch(new ScreenPoint(region.X, region.Y), expected)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingInputSimulator : IInputSimulator, IInputSimulatorCapabilities
    {
        private readonly List<string> _activity;

        public RecordingInputSimulator(List<string> activity)
        {
            _activity = activity;
        }

        public string ProviderName => "recording-input-simulator";

        public bool IsSupported => true;

        public bool SupportsAbsoluteCoordinates { get; init; } = true;

        public int InitializedWidth { get; private set; }

        public int InitializedHeight { get; private set; }

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializedWidth = screenWidth;
            InitializedHeight = screenHeight;
        }

        public void MoveAbsolute(int x, int y)
        {
            _activity.Add($"input:move-abs:{x},{y}");
        }

        public void MoveRelative(int dx, int dy)
        {
            _activity.Add($"input:move:{dx},{dy}");
        }

        public void MouseButton(int button, bool pressed)
        {
            if (pressed)
            {
                var name = button switch
                {
                    MouseButtonCode.Right => "right",
                    MouseButtonCode.Middle => "middle",
                    _ => "left"
                };
                _activity.Add($"input:click:{name}");
            }
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<int> WaitCalls { get; } = [];

        public async Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMs);
            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class DisposalTrackingFrameProvider : IScreenFrameProvider
    {
        private readonly Queue<ScreenPixelColor> _colors;

        public DisposalTrackingFrameProvider(params ScreenPixelColor[] colors)
        {
            _colors = new Queue<ScreenPixelColor>(colors);
        }

        public string ProviderName => "disposal-tracking-frame-provider";

        public bool IsSupported => true;

        public int CaptureCalls { get; private set; }

        public Action? AfterCapture { get; init; }

        public List<CountingDisposable> Owners { get; } = [];

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            CaptureCalls++;

            var bounds = region ?? new ScreenRect(1, 2, 1, 1);
            var owner = new CountingDisposable();
            Owners.Add(owner);
            var frame = CreateFrame(bounds, _colors.Dequeue(), owner);
            AfterCapture?.Invoke();
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(frame));
        }

        public void Dispose()
        {
        }

        private static ScreenFrame CreateFrame(ScreenRect bounds, ScreenPixelColor color, IDisposable owner)
        {
            var pixels = new[] { color.B, color.G, color.R, (byte)0x00 };
            return new ScreenFrame(bounds, 4, ScreenPixelFormat.Xrgb8888, pixels, owner);
        }
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
