// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{

    [Fact]
    public async Task PlayAsync_WhenScreenReadingScriptSteps_SamplesAndStoresRuntimeVariables()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
            RelativePixelColor = new ScreenPixelColor(0xAA, 0xBB, 0xCC),
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33)),
        };
        var positionProvider = CreatePositionProvider((50, 60));
        using var player = CreatePlayer(positionProvider, screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 10 20 sampled",
                "pixelcolor rel 5 -3 relativeSampled",
                "waitcolor 1 2 00FF00 123",
                "pixelsearch 0 0 10 12 112233 found_x found_y tolerance 10",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.GetPixelPoints.Should().Equal(new ScreenPoint(10, 20), new ScreenPoint(55, 57));
        _ = screenReader.WaitCalls.Should().ContainSingle(call =>
            call.Point == new ScreenPoint(1, 2)
            && call.Expected == new ScreenPixelColor(0x00, 0xFF, 0x00)
            && call.Options.Timeout == TimeSpan.FromMilliseconds(123));
        _ = screenReader.SearchCalls.Should().ContainSingle(call =>
            call.Region.X == 0
            && call.Region.Y == 0
            && call.Region.Width == 10
            && call.Region.Height == 12
            && call.Expected == new ScreenPixelColor(0x11, 0x22, 0x33)
            && call.Tolerance == 10);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("sampled", "123456");
        _ = variables.Should().Contain("relativeSampled", "AABBCC");
        _ = variables.Should().Contain("found_x", "7");
        _ = variables.Should().Contain("found_y", "8");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorUsesVariableTargetColor_PassesSampledColorToScreenReader()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 1 2 sampled",
                "waitcolor 3 4 $sampled 100 wait_ok",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.WaitCalls.Should().ContainSingle(call =>
            call.Point == new ScreenPoint(3, 4)
            && call.Expected == new ScreenPixelColor(0x12, 0x34, 0x56)
            && call.Options.Timeout == TimeSpan.FromMilliseconds(100));

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("sampled", "123456");
        _ = variables.Should().Contain("wait_ok", "true");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchUsesVariableTargetColor_PassesSampledColorToScreenReader()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33)),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 1 2 sampled",
                "pixelsearch 0 0 10 12 $sampled found found_x found_y tolerance 10",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.SearchCalls.Should().ContainSingle(call =>
            call.Region == new ScreenRect(0, 0, 10, 12)
            && call.Expected == new ScreenPixelColor(0x12, 0x34, 0x56)
            && call.Tolerance == 10);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("sampled", "123456");
        _ = variables.Should().Contain("found", "true");
        _ = variables.Should().Contain("found_x", "7");
        _ = variables.Should().Contain("found_y", "8");
    }

    [Fact]
    public async Task PlayAsync_WhenScreenReadingOnlyScriptLoops_RepeatsScriptSteps()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0x12, 0x34, 0x56),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "pixelcolor 10 20 sampled" },
        };

        await player.PlayAsync(macro, new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 3,
        }, CancellationToken.None);

        _ = screenReader.GetPixelPoints.Should().Equal(
            new ScreenPoint(10, 20),
            new ScreenPoint(10, 20),
            new ScreenPoint(10, 20));
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
                new RunScriptStep("waitcolor 3 4 FFFFFF 10"),
            ]);

        _ = compileResult.Success.Should().BeTrue(compileResult.ErrorMessage);

        await player.PlayAsync(compileResult.Sequence!, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
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
            {
                "pixelcolor 1 2 sampled",
                "delay 1",
                "click left",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
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
            {
                "pixelcolor 1 2 sampled",
                "if $sampled == 123456 {",
                "click left",
                "}",
                "else {",
                "move rel 9 9",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelColorFeedsLowercaseHexWhileCondition_ExecutesMatchingRuntimeBranch()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            PixelColor = new ScreenPixelColor(0x1C, 0x1C, 0x1C),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 1 2 sampled",
                "while $sampled == 1c1c1c {",
                "click left",
                "break",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:click:left");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorHasResultVariable_StoresFalseAndContinuesOnTimeout()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.CaptureTimeout,
                "waitcolor timed out"),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "waitcolor 3 4 FFFFFF 10 wait_ok",
                "if $wait_ok == false {",
                "click left",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("wait_ok", "false");
        _ = activity.Should().Equal(
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
            ScriptSteps = { "waitcolor 3 4 $sampled 100 wait_ok" },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
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
            {
                "set sampled not-a-color",
                "waitcolor 3 4 $sampled 100 wait_ok",
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 2: color variable 'sampled' value 'not-a-color' is invalid. Expected RRGGBB.");
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasFoundVariable_StoresFalseCoordinatesAndContinuesOnNoMatch()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            SearchResult = ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                "pixelsearch found no matching pixel"),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "if $found == false {",
                "click left",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("found", "false");
        _ = variables.Should().Contain("found_x", "-1");
        _ = variables.Should().Contain("found_y", "-1");
        _ = activity.Should().Equal(
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
            SearchMatch = new ScreenPixelSearchMatch(new ScreenPoint(7, 8), new ScreenPixelColor(0x11, 0x22, 0x33)),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { step },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.SearchCalls.Should().ContainSingle(call => call.Tolerance == expectedTolerance);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        if (foundVariable is not null)
        {
            _ = variables.Should().Contain(foundVariable, "true");
        }

        if (xVariable is not null && yVariable is not null)
        {
            _ = variables.Should().Contain(xVariable, "7");
            _ = variables.Should().Contain(yVariable, "8");
        }
        else
        {
            _ = variables.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorTimesOut_ThrowsRuntimeFailure()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.CaptureTimeout,
                "waitcolor timed out"),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "waitcolor 1 2 FFFFFF 10" },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*waitcolor failed: CaptureTimeout: waitcolor timed out*");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorReturnsCanceled_StopsCleanly()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled"),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "waitcolor 1 2 FFFFFF 10" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.WaitCalls.Should().ContainSingle();
        _ = player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorHasResultVariable_ReturnsCanceled_StopsCleanly()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled"),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "waitcolor 1 2 FFFFFF 10 wait_ok",
                "click left",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal("screen:waitcolor:1,2");
        _ = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
        _ = player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitColorReturnsCanceled_DoesNotRunLaterScreenReads()
    {
        var screenReader = new FakeScreenPixelReader
        {
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.Canceled,
                "waitcolor canceled"),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "waitcolor 1 2 FFFFFF 10",
                "pixelsearch 0 0 1 1 FFFFFF found_x found_y",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.WaitCalls.Should().ContainSingle();
        _ = screenReader.SearchCalls.Should().BeEmpty();
        _ = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasFoundVariable_ReturnsCanceled_StopsCleanly()
    {
        var activity = new List<string>();
        var screenReader = new RecordingScreenPixelReader(activity)
        {
            SearchResult = ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                ScreenReadErrorKind.Canceled,
                "pixelsearch canceled"),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "click left",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal("screen:pixelsearch:0,0");
        _ = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
        _ = player.IsPlaying.Should().BeFalse();
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

        _ = result.IsSuccess.Should().BeTrue();
        _ = provider.CaptureCalls.Should().Be(2);
        _ = provider.Owners.Should().AllSatisfy(owner => owner.DisposeCount.Should().Be(1));
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

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
        _ = provider.CaptureCalls.Should().Be(1);
        _ = provider.Owners.Should().ContainSingle(owner => owner.DisposeCount == 1);
    }

    [Fact]
    public async Task WaitForPixelAsync_WhenCanceledAfterCapture_DisposesCapturedFrameAndReturnsCanceled()
    {
        using var cts = new CancellationTokenSource();
        var provider = new DisposalTrackingFrameProvider(new ScreenPixelColor(0x00, 0x00, 0x00))
        {
            AfterCapture = () => cts.Cancel(),
        };
        using var reader = new ScreenPixelReader(provider);

        var result = await reader.WaitForPixelAsync(
            new ScreenPoint(1, 2),
            new ScreenPixelColor(0x00, 0xFF, 0x00),
            new ScreenReadOptions(
                timeout: TimeSpan.FromMinutes(1),
                pollInterval: TimeSpan.FromMinutes(1),
                cancellationToken: cts.Token));

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.Canceled);
        _ = provider.CaptureCalls.Should().Be(1);
        _ = provider.Owners.Should().ContainSingle(owner => owner.DisposeCount == 1);
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

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.OutOfBounds);
    }

    [Fact]
    public async Task PlayAsync_WhenPixelSearchHasNoMatch_ThrowsRuntimeFailure()
    {
        var screenReader = new FakeScreenPixelReader
        {
            SearchResult = ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                "pixelsearch found no matching pixel"),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "pixelsearch 0 0 1 1 FFFFFF found_x found_y" },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pixelsearch failed: CaptureTimeout: pixelsearch found no matching pixel*");

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().NotContainKey("found_x");
        _ = variables.Should().NotContainKey("found_y");
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
            WaitResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(errorKind, errorMessage),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "waitcolor 3 4 FFFFFF 10 wait_ok",
                "click left",
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*waitcolor failed: {errorKind}: {errorMessage}*");

        _ = activity.Should().Equal("screen:waitcolor:3,4");
        _ = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
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
            SearchResult = ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(errorKind, errorMessage),
        };
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader, inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelsearch 0 0 10 12 112233 found found_x found_y tolerance 10",
                "click left",
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*pixelsearch failed: {errorKind}: {errorMessage}*");

        _ = activity.Should().Equal("screen:pixelsearch:0,0");
        _ = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenPixelColorUsesLowerValue_StoresCanonicalUppercaseRgb()
    {
        var screenReader = new FakeScreenPixelReader
        {
            PixelColor = new ScreenPixelColor(0xAB, 0xCD, 0xEF),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "pixelcolor 10 20 color" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("color", "ABCDEF");
    }
}
