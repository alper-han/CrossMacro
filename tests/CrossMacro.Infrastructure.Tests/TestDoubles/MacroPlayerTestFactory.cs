using System.Diagnostics;
using CrossMacro.Platform.Abstractions.ScreenCapture;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>Single place that knows the full <see cref="MacroPlayerDependencies"/> argument list.</summary>
internal static class MacroPlayerTestFactory
{
    public static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider? positionProvider,
        IKeyCodeMapper keyCodeMapper,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null,
        IScreenImageSearchReader? screenPixelReader = null,
        IClipboardService? clipboardService = null,
        IShellCommandRunner? shellCommandRunner = null,
        IScreenshotCaptureService? screenshotCaptureService = null)
    {
        return new MacroPlayerDependencies(
            positionProvider,
            timingService ?? new SystemPlaybackTimingService(),
            playbackWaitAsync ?? ((_, _) => Task.CompletedTask),
            playbackElapsedMillisecondsFactory ?? CreateElapsedMillisecondsProvider,
            inputSimulatorFactory,
            simulatorPool: null,
            screenPixelReader ?? NullScreenPixelReader.Instance,
            keyCodeMapper,
            new NullWindowManager(),
            clipboardService,
            shellCommandRunner,
            screenshotCaptureService,
            new ImageClickMovementResolver(positionProvider),
            new ImageAssetCodec());
    }

    public static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>A mapper that knows no key names, modifiers or characters.</summary>
    public static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }
}
