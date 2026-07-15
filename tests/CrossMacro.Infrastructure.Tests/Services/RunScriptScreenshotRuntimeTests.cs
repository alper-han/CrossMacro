using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

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

        service.Calls.Should().ContainSingle().Which.Should().Be(("path with spaces.png", true, new ScreenRect(1, 2, 30, 40)));
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenCaptureServiceFails_ThrowsStepContext()
    {
        var service = new RecordingScreenshotCaptureService
        {
            Result = ScreenshotCaptureResult.Fail(ScreenshotCaptureFailureKind.CaptureFailed, "capture failed", ["portal denied"]),
        };
        var executor = new RunScriptScreenshotExecutor(service);

        var act = async () => await executor.ExecuteStepAsync("screenshot clipboard", 7, new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
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
            ScriptSteps = ["screenshot output simple.png"],
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        service.Calls.Should().ContainSingle().Which.Should().Be(("simple.png", false, null));
    }

    private static MacroPlayer CreatePlayer(
        IScreenshotCaptureService screenshotCaptureService,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.ProviderName.Returns("fake-position");
        positionProvider.IsSupported.Returns(returnThis: true);

        return new MacroPlayer(
            positionProvider,
            new PlaybackValidator(keyCodeMapper, positionProvider),
            playbackWaitAsync: (_, _) => Task.CompletedTask,
            inputSimulatorFactory: inputSimulatorFactory ?? (() => Substitute.For<IInputSimulator>()),
            keyCodeMapper: keyCodeMapper,
            screenshotCaptureService: screenshotCaptureService);
    }

    private sealed class RecordingScreenshotCaptureService : IScreenshotCaptureService
    {
        public ScreenshotCaptureResult Result { get; init; } = ScreenshotCaptureResult.Ok(new ScreenshotCaptureData("out.png", 10, 20, "png", "fake", IsRegion: false, CopiedToClipboard: false));

        public List<(string? OutputPath, bool CopyToClipboard, ScreenRect? Region)> Calls { get; } = [];

        public Task<ScreenshotCaptureResult> CaptureAsync(string? outputPath, bool copyToClipboard, ScreenRect? region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((outputPath, copyToClipboard, region));
            return Task.FromResult(Result);
        }
    }
}
