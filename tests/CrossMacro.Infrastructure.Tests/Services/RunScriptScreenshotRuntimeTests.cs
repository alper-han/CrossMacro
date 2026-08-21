
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptScreenshotRuntimeTests
{
    [Fact]
    public async Task ExecuteStepAsync_WhenVariablesAreUsed_ResolvesOutputAndRegionBeforeCapture()
    {
        var service = new RecordingScreenshotCaptureService();
        var executor = new RunScriptScreenshotExecutor(service);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "path with spaces.png",
            ["x"] = "1",
            ["y"] = "2",
            ["w"] = "30",
            ["h"] = "40",
        };

        await executor.ExecuteStepAsync("screenshot region $x $y $w $h output \"$name\" clipboard", 5, variables, CancellationToken.None);

        _ = service.Calls.Should().ContainSingle().Which.Should().Be(("path with spaces.png", true, new ScreenRect(1, 2, 30, 40)));
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureServiceFails_ThrowsStepContext()
    {
        var service = new RecordingScreenshotCaptureService
        {
            Result = ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.CaptureFailed, "capture failed", ["portal denied"]),
        };
        var executor = new RunScriptScreenshotExecutor(service);

        var act = async () => await executor.ExecuteStepAsync("screenshot clipboard", 7, new Dictionary<string, string>(StringComparer.Ordinal), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 7: capture failed portal denied");
    }

    [Fact]
    public async Task PlayAsync_WhenScreenshotOnlyScript_RunsWithoutSimulator()
    {
        var service = new RecordingScreenshotCaptureService();
        using var player = CreatePlayer(
            service,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"));
        var macro = new MacroSequence
        {
            ScriptSteps = { "screenshot output simple.png" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = service.Calls.Should().ContainSingle().Which.Should().Be(("simple.png", false, null));
    }

    private static MacroPlayer CreatePlayer(
        IScreenshotCaptureService screenshotCaptureService,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);

        return new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, positionProvider),
            CreateDependencies(
            positionProvider,
            keyCodeMapper,
            inputSimulatorFactory ?? (() => Substitute.For<IInputSimulator>()),
            screenshotCaptureService));
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider positionProvider,
        IKeyCodeMapper keyCodeMapper,
        Func<IInputSimulator> inputSimulatorFactory,
        IScreenshotCaptureService screenshotCaptureService)
    {
        return new MacroPlayerDependencies(positionProvider, new SystemPlaybackTimingService(), (_, _) => Task.CompletedTask,
            CreateElapsedMillisecondsProvider, () => new DefaultPlaybackCoordinator(positionProvider), () => new ButtonStateTracker(),
            () => new KeyStateTracker(), new DefaultPlaybackMouseButtonMapper(), inputSimulatorFactory, simulatorPool: null,
            NullScreenPixelReader.Instance, keyCodeMapper, new NullWindowManager(), clipboardService: null, shellCommandRunner: null,
            screenshotCaptureService, new ImageClickMovementResolver(positionProvider), new ImageAssetCodec(), new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    private sealed class RecordingScreenshotCaptureService : IScreenshotCaptureService
    {
        public ScreenshotCaptureResult Result { get; init; } = ScreenshotCaptureResult.Ok(new ScreenshotCaptureData("out.png", 10, 20, "png", "fake", IsRegion: false, CopiedToClipboard: false));

        public List<(string? OutputPath, bool CopyToClipboard, ScreenRect? Region)> Calls { get; } = [];

        public Task<ScreenshotPngCaptureResult> CapturePngAsync(ScreenshotPngCaptureRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ProviderUnsupported,
                "In-memory screenshot capture is not configured for this fake.",
                []));
        }

        public Task<ScreenshotCaptureResult> CaptureAsync(string? outputPath, bool copyToClipboard, ScreenRect? region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((outputPath, copyToClipboard, region));
            return Task.FromResult(Result);
        }
    }
}
