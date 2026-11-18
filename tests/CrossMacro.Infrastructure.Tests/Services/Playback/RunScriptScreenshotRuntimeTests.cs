
namespace CrossMacro.Infrastructure.Tests.Services.Playback;

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

        await executor.ExecuteStepAsync("screenshot region $x $y $w $h output \"$name\" clipboard", 5, variables, TestContext.Current.CancellationToken);

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

        var act = async () => await executor.ExecuteStepAsync("screenshot clipboard", 7, new Dictionary<string, string>(StringComparer.Ordinal), TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 7: capture failed portal denied");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureIsCanceled_PropagatesCancellationWithoutWrapping()
    {
        var service = new RecordingScreenshotCaptureService();
        var executor = new RunScriptScreenshotExecutor(service);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await executor.ExecuteStepAsync(
            "screenshot clipboard",
            8,
            new Dictionary<string, string>(StringComparer.Ordinal),
            cancellation.Token);

        _ = await act.Should().ThrowAsync<OperationCanceledException>();
        _ = service.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureServiceIsMissing_ThrowsStepContext()
    {
        var executor = new RunScriptScreenshotExecutor(screenshotCaptureService: null);

        var act = async () => await executor.ExecuteStepAsync(
            "screenshot clipboard",
            9,
            new Dictionary<string, string>(StringComparer.Ordinal),
            TestContext.Current.CancellationToken);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 9: Screenshot script steps require an IScreenshotCaptureService runtime service.");
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

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = service.Calls.Should().ContainSingle().Which.Should().Be(("simple.png", false, null));
    }

    private static MacroPlayer CreatePlayer(
        IScreenshotCaptureService screenshotCaptureService,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        var keyCodeMapper = MacroPlayerTestFactory.CreateKeyCodeMapper();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);

        return new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, positionProvider),
            MacroPlayerTestFactory.CreateDependencies(
                positionProvider,
                keyCodeMapper,
                inputSimulatorFactory ?? (() => Substitute.For<IInputSimulator>()),
                screenshotCaptureService: screenshotCaptureService));
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
