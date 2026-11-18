using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public abstract class MacroPlayerTestBase
{
    internal static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);
    internal readonly IMousePositionProvider _positionProvider;
    internal readonly IKeyCodeMapper _keyCodeMapper;
    internal readonly PlaybackValidator _validator;

    protected MacroPlayerTestBase()
    {
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _ = _positionProvider.IsSupported.Returns(returnThis: true);
        _ = _positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        _keyCodeMapper = MacroPlayerTestFactory.CreateKeyCodeMapper();
        _validator = new PlaybackValidator(_keyCodeMapper, _positionProvider);
    }

    internal MacroPlayer CreatePlayer(
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IPlaybackTimingService? timingService = null,
        Func<TimeSpan, CancellationToken, Task>? playbackWaitAsync = null,
        Func<Func<double>>? playbackElapsedMillisecondsFactory = null,
        IPlaybackValidator? validator = null)
    {
        return new MacroPlayer(validator ?? _validator, MacroPlayerTestFactory.CreateDependencies(
            _positionProvider,
            _keyCodeMapper,
            inputSimulatorFactory,
            timingService,
            playbackWaitAsync,
            playbackElapsedMillisecondsFactory));
    }

    internal sealed class ControlledTimingService : IPlaybackTimingService
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

    internal sealed class ManualPlaybackClock
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

    internal class TrackingInputSimulator(bool forceRelativeOnly = false) : IInputSimulator, IInputSimulatorCapabilities
    {
        public string ProviderName => "Tracking";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates => !forceRelativeOnly && InitializedWidth > 0 && InitializedHeight > 0;
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
            Operations.Add(string.Create(CultureInfo.InvariantCulture, $"abs:{x},{y}"));
        }

        public void MoveRelative(int dx, int dy)
        {
            Operations.Add(string.Create(CultureInfo.InvariantCulture, $"rel:{dx},{dy}"));
        }

        public void MouseButton(int button, bool pressed)
        {
            ButtonTransitions.Add((button, pressed));
            Operations.Add(pressed ? "btn:down" : "btn:up");
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
            ScrollOperations.Add((delta, isHorizontal));
            Operations.Add(string.Create(CultureInfo.InvariantCulture, $"scroll:{delta},{isHorizontal}"));
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

    internal sealed class TrackingTrajectoryInputSimulator : TrackingInputSimulator, IAbsoluteMotionTrajectorySimulator
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
