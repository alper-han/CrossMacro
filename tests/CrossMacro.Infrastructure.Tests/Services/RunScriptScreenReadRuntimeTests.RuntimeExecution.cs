// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{

    [Fact]
    public async Task PlayAsync_WhenRuntimeOnlyClipboardDelayAndVariables_DoesNotAcquireSimulator()
    {
        var timingService = new RecordingTimingService();
        var clipboard = Substitute.For<IClipboardService>();
        _ = clipboard.IsSupported.Returns(returnThis: true);
        _ = clipboard.GetTextAsync(Arg.Any<CancellationToken>()).Returns("clipboard text");
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new FakeScreenPixelReader(),
            timingService: timingService,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"),
            clipboardService: clipboard);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "clipboard set \"hello   spaced   world\"",
                "delay 5",
                "clipboard get $clip",
                "set copied=$clip",
                "clipboard set $copied",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("hello   spaced   world", Arg.Any<CancellationToken>());
        await clipboard.Received(1).SetTextAsync("clipboard text", Arg.Any<CancellationToken>());
        _ = timingService.WaitCalls.Should().ContainSingle().Which.Should().Be(5);
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeOnlyControlFlow_DoesNotAcquireSimulator()
    {
        var clipboard = Substitute.For<IClipboardService>();
        _ = clipboard.IsSupported.Returns(returnThis: true);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new FakeScreenPixelReader(),
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"),
            clipboardService: clipboard);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "set n=1",
                "if $n == 1 {",
                "clipboard set ok",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("ok", Arg.Any<CancellationToken>());
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
            {
                "pixelcolor 1 2 sampled",
                "move absolute 100 200",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = inputSimulator.InitializedWidth.Should().Be(1920);
        _ = inputSimulator.InitializedHeight.Should().Be(1080);
        _ = activity.Should().Equal(
            "screen:pixelcolor:1,2",
            "input:move-abs:100,200");
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
            {
                "pixelcolor 1 2 sampled",
                "set Count=1",
                "inc count 2",
                "set Amount 4",
                "dec COUNT $Amount",
                "set combined $count",
                "if $COUNT == -1 {",
                "click left",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables["COUNT"].Should().Be("-1");
        _ = variables["combined"].Should().Be("-1");
        _ = activity.Should().Equal(
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
            {
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
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
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
            {
                "pixelcolor 1 2 sampled",
                "repeat 2 {",
                "if $sampled == FFFFFF {",
                "move rel 1 1",
                "}",
                "else {",
                "move rel 2 2",
                "}",
                "click left",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal(
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
            {
                "pixelcolor 1 2 sampled",
                "set wait_ms 25",
                "delay $wait_ms",
                "click left",
            },
        };

        await player.PlayAsync(macro, new PlaybackOptions { SpeedMultiplier = 2.5 }, CancellationToken.None);

        _ = timingService.WaitCalls.Should().Equal(10);
        _ = activity.Should().Equal(
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
            {
                "pixelcolor 1 2 sampled",
                "if $missing == 1 {",
                "click left",
                "}",
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unknown variable '$missing'.");
        _ = activity.Should().Equal("screen:pixelcolor:1,2");
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
            {
                "pixelcolor 1 2 sampled",
                "for i from 1 to 3 step 0 {",
                "click left",
                "}",
            },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("For step cannot be 0.");
        _ = activity.Should().Equal("screen:pixelcolor:1,2");
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
            ScriptSteps = { "pixelcolor 1 2 sampled" },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: cts.Token);

        _ = await act.Should().ThrowAsync<OperationCanceledException>();
        _ = screenReader.GetPixelPoints.Should().BeEmpty();
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
            ScriptSteps = { "set c=123456" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("c", "123456");
    }
}
