namespace CrossMacro.Infrastructure.Tests.Services;


/// <summary>
/// Tests for MacroPlayer focusing on edge cases and error handling
/// </summary>
public sealed partial class MacroPlayerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);
    private readonly IMousePositionProvider _positionProvider;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly PlaybackValidator _validator;

    public MacroPlayerTests()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _ = _positionProvider.IsSupported.Returns(returnThis: true);
        _ = _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _keyCodeMapper = CreateKeyCodeMapper();
        _validator = new PlaybackValidator(_keyCodeMapper, _positionProvider);
    }

    private MacroPlayer CreatePlayer(
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null,
        IPlaybackValidator? validator = null)
    {
        return new MacroPlayer(validator ?? _validator, CreateDependencies(
            _positionProvider,
            inputSimulatorFactory,
            timingService,
            playbackWaitAsync ?? ((_, _) => Task.CompletedTask),
            playbackElapsedMillisecondsFactory,
            _keyCodeMapper));
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider? positionProvider,
        Func<IInputSimulator>? inputSimulatorFactory,
        IPlaybackTimingService? timingService,
        Func<TimeSpan, CancellationToken, Task> playbackWaitAsync,
        Func<Func<double>>? playbackElapsedMillisecondsFactory,
        IKeyCodeMapper keyCodeMapper,
        IScreenPixelReader? screenPixelReader = null,
        IClipboardService? clipboardService = null,
        IShellCommandRunner? shellCommandRunner = null,
        IScreenshotCaptureService? screenshotCaptureService = null)
    {
        return new MacroPlayerDependencies(
            positionProvider,
            timingService ?? new SystemPlaybackTimingService(),
            playbackWaitAsync,
            playbackElapsedMillisecondsFactory ?? CreateElapsedMillisecondsProvider,
            () => new DefaultPlaybackCoordinator(positionProvider),
            () => new ButtonStateTracker(),
            () => new KeyStateTracker(),
            new DefaultPlaybackMouseButtonMapper(),
            inputSimulatorFactory,
            simulatorPool: null,
            screenPixelReader ?? NullScreenPixelReader.Instance,
            keyCodeMapper,
            new NullWindowManager(),
            clipboardService,
            shellCommandRunner,
            screenshotCaptureService,
            new ImageClickMovementResolver(positionProvider),
            new ImageAssetCodec(),
            new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }





























































    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<double> WaitCalls { get; } = new();
        public TaskCompletionSource<bool>? WaitEntered { get; set; }
        public TaskCompletionSource<bool>? ContinueWait { get; set; }

        public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMilliseconds);
            _ = (WaitEntered?.TrySetResult(true));

            if (ContinueWait is not null)
            {
                _ = await ContinueWait.Task.WaitAsync(cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class ControlledTimingService : IPlaybackTimingService
    {
        public List<double> WaitCalls { get; } = new();
        public Func<int, double, IPlaybackPauseToken, CancellationToken, Task>? OnWaitAsync { get; set; }
        private int _waitCallCount;

        public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMilliseconds);
            int callIndex = ++_waitCallCount;
            if (OnWaitAsync is not null)
            {
                await OnWaitAsync(callIndex, delayMilliseconds, pauseToken, cancellationToken);
            }

            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class ManualPlaybackClock
    {
        private double _elapsedMilliseconds;

        public void AdvanceBy(double milliseconds)
        {
            _elapsedMilliseconds += milliseconds;
        }

        public Func<Func<double>> CreateElapsedMillisecondsProviderFactory()
        {
            return () => () => _elapsedMilliseconds;
        }
    }

    private class TrackingInputSimulator(bool forceRelativeOnly = false) : IInputSimulator, IInputSimulatorCapabilities
    {
        public string ProviderName => "Tracking";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates { get => !field && InitializedWidth > 0 && InitializedHeight > 0; } = forceRelativeOnly;
        public int InitializedWidth { get; private set; }
        public int InitializedHeight { get; private set; }
        public List<(int X, int Y)> AbsoluteMoves { get; } = new();
        public List<(int Button, bool Pressed)> ButtonTransitions { get; } = new();
        public List<(int Delta, bool IsHorizontal)> ScrollOperations { get; } = new();
        public List<string> Operations { get; } = new();

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializedWidth = screenWidth;
            InitializedHeight = screenHeight;
        }

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y)
        {
            AbsoluteMoves.Add((x, y));
            Operations.Add($"abs:{x},{y}");
        }

        public void MoveRelative(int dx, int dy)
        {
            Operations.Add($"rel:{dx},{dy}");
        }

        public void MouseButton(int button, bool pressed)
        {
            ButtonTransitions.Add((button, pressed));
            Operations.Add(pressed ? "btn:down" : "btn:up");
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
            ScrollOperations.Add((delta, isHorizontal));
            Operations.Add($"scroll:{delta},{isHorizontal}");
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

    private sealed class TrackingTrajectoryInputSimulator : TrackingInputSimulator, IAbsoluteMotionTrajectorySimulator
    {
        public List<IReadOnlyList<AbsoluteMotionTrajectorySample>> Trajectories { get; } = [];

        public Task SimulateAbsoluteTrajectoryAsync(
            IReadOnlyList<AbsoluteMotionTrajectorySample> samples,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Trajectories.Add(samples.ToArray());
            return Task.CompletedTask;
        }
    }
}
