
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Platform services and time seams a <see cref="MacroPlayer"/> needs. Playback-internal collaborators
/// (coordinator, input state trackers, button mapper, delay resolver) are created by the player itself.
/// </summary>
public sealed class MacroPlayerDependencies(
    IMousePositionProvider? positionProvider,
    IPlaybackTimingService timingService,
    Func<TimeSpan, CancellationToken, Task> playbackWaitAsync,
    Func<Func<double>> playbackElapsedMillisecondsFactory,
    Func<IInputSimulator>? inputSimulatorFactory,
    IInputSimulatorPool? simulatorPool,
    IScreenImageSearchReader screenPixelReader,
    IKeyCodeMapper keyCodeMapper,
    IWindowManager windowManager,
    IClipboardService? clipboardService,
    IShellCommandRunner? shellCommandRunner,
    IScreenshotCaptureService? screenshotCaptureService,
    IImageClickMovementResolver imageClickMovementResolver,
    IImageAssetCodec imageAssetCodec)
{
    public IMousePositionProvider? PositionProvider { get; } = positionProvider;
    public IPlaybackTimingService TimingService { get; } = timingService ?? throw new ArgumentNullException(nameof(timingService));
    public Func<TimeSpan, CancellationToken, Task> PlaybackWaitAsync { get; } = playbackWaitAsync ?? throw new ArgumentNullException(nameof(playbackWaitAsync));
    public Func<Func<double>> PlaybackElapsedMillisecondsFactory { get; } = playbackElapsedMillisecondsFactory ?? throw new ArgumentNullException(nameof(playbackElapsedMillisecondsFactory));
    public Func<IInputSimulator>? InputSimulatorFactory { get; } = inputSimulatorFactory;
    public IInputSimulatorPool? SimulatorPool { get; } = simulatorPool;
    public IScreenImageSearchReader ScreenPixelReader { get; } = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
    public IKeyCodeMapper KeyCodeMapper { get; } = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    public IWindowManager WindowManager { get; } = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
    public IClipboardService? ClipboardService { get; } = clipboardService;
    public IShellCommandRunner? ShellCommandRunner { get; } = shellCommandRunner;
    public IScreenshotCaptureService? ScreenshotCaptureService { get; } = screenshotCaptureService;
    public IImageClickMovementResolver ImageClickMovementResolver { get; } = imageClickMovementResolver ?? throw new ArgumentNullException(nameof(imageClickMovementResolver));
    public IImageAssetCodec ImageAssetCodec { get; } = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));
}
