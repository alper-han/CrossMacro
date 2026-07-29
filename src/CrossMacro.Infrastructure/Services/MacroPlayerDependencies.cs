
namespace CrossMacro.Infrastructure.Services;

public sealed class MacroPlayerDependencies(
    IMousePositionProvider? positionProvider,
    IPlaybackTimingService timingService,
    Func<TimeSpan, CancellationToken, Task> playbackWaitAsync,
    Func<Func<double>> playbackElapsedMillisecondsFactory,
    Func<IPlaybackCoordinator> coordinatorFactory,
    Func<IButtonStateTracker> buttonTrackerFactory,
    Func<IKeyStateTracker> keyTrackerFactory,
    IPlaybackMouseButtonMapper buttonMapper,
    Func<IInputSimulator>? inputSimulatorFactory,
    IInputSimulatorPool? simulatorPool,
    IPlaybackBehaviorPolicy playbackBehaviorPolicy,
    IScreenPixelReader screenPixelReader,
    IKeyCodeMapper keyCodeMapper,
    IWindowManager windowManager,
    IClipboardService? clipboardService,
    IShellCommandRunner? shellCommandRunner,
    IScreenshotCaptureService? screenshotCaptureService,
    IImageClickMovementResolver imageClickMovementResolver,
    IImageAssetCodec imageAssetCodec,
    PlaybackDelayResolver delayResolver)
{
    public IMousePositionProvider? PositionProvider { get; } = positionProvider;
    public IPlaybackTimingService TimingService { get; } = timingService ?? throw new ArgumentNullException(nameof(timingService));
    public Func<TimeSpan, CancellationToken, Task> PlaybackWaitAsync { get; } = playbackWaitAsync ?? throw new ArgumentNullException(nameof(playbackWaitAsync));
    public Func<Func<double>> PlaybackElapsedMillisecondsFactory { get; } = playbackElapsedMillisecondsFactory ?? throw new ArgumentNullException(nameof(playbackElapsedMillisecondsFactory));
    public Func<IPlaybackCoordinator> CoordinatorFactory { get; } = coordinatorFactory ?? throw new ArgumentNullException(nameof(coordinatorFactory));
    public Func<IButtonStateTracker> ButtonTrackerFactory { get; } = buttonTrackerFactory ?? throw new ArgumentNullException(nameof(buttonTrackerFactory));
    public Func<IKeyStateTracker> KeyTrackerFactory { get; } = keyTrackerFactory ?? throw new ArgumentNullException(nameof(keyTrackerFactory));
    public IPlaybackMouseButtonMapper ButtonMapper { get; } = buttonMapper ?? throw new ArgumentNullException(nameof(buttonMapper));
    public Func<IInputSimulator>? InputSimulatorFactory { get; } = inputSimulatorFactory;
    public IInputSimulatorPool? SimulatorPool { get; } = simulatorPool;
    public IPlaybackBehaviorPolicy PlaybackBehaviorPolicy { get; } = playbackBehaviorPolicy ?? throw new ArgumentNullException(nameof(playbackBehaviorPolicy));
    public IScreenPixelReader ScreenPixelReader { get; } = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
    public IKeyCodeMapper KeyCodeMapper { get; } = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    public IWindowManager WindowManager { get; } = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
    public IClipboardService? ClipboardService { get; } = clipboardService;
    public IShellCommandRunner? ShellCommandRunner { get; } = shellCommandRunner;
    public IScreenshotCaptureService? ScreenshotCaptureService { get; } = screenshotCaptureService;
    public IImageClickMovementResolver ImageClickMovementResolver { get; } = imageClickMovementResolver ?? throw new ArgumentNullException(nameof(imageClickMovementResolver));
    public IImageAssetCodec ImageAssetCodec { get; } = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));
    internal PlaybackDelayResolver DelayResolver { get; } = delayResolver ?? throw new ArgumentNullException(nameof(delayResolver));
}
