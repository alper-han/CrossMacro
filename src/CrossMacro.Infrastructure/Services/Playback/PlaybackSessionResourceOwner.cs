
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns resources whose lifetime is exactly one playback session.
/// </summary>
internal sealed class PlaybackSessionResourceOwner : IDisposable, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly Func<TimeSpan, CancellationToken, Task> _waitAsync;
    private readonly Func<IInputSimulator>? _simulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool;
    private readonly ManualResetEventSlim _pauseEvent = new(initialState: true);
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private IInputSimulator? _simulator;
    private int _released;
    private int _pauseVersion;
    private bool _isPaused;
    private ushort[] _pausedButtons = Array.Empty<ushort>();
    private int[] _pausedKeys = Array.Empty<int>();
    private CancellationTokenSource? _cancellation;
    private int _width;
    private int _height;

    public PlaybackSessionResourceOwner(
        Func<TimeSpan, CancellationToken, Task> waitAsync,
        Func<IInputSimulator>? simulatorFactory,
        IInputSimulatorPool? simulatorPool)
    {
        _waitAsync = waitAsync ?? throw new ArgumentNullException(nameof(waitAsync));
        _simulatorFactory = simulatorFactory;
        _simulatorPool = simulatorPool;
    }

    public bool IsPlaying { get; private set; }
    public bool IsPaused => _isPaused;
    public int PauseResumeVersion => Volatile.Read(ref _pauseVersion);
    public IInputSimulator? Simulator => _simulator;
    public CancellationToken Token => _cancellation?.Token ?? CancellationToken.None;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _variables;
    public IDictionary<string, string> Variables => _variables;

    public void Begin(CancellationToken cancellationToken)
    {
        if (IsPlaying)
        {
            throw new InvalidOperationException("Playback is already in progress");
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _variables.Clear();
        _isPaused = false;
        Volatile.Write(ref _pauseVersion, 0);
        _pauseEvent.Set();
        IsPlaying = true;
    }

    public async Task AcquireAsync(int width, int height, CancellationToken cancellationToken)
    {
        _width = width;
        _height = height;
        if (_simulatorPool is not null)
        {
            _simulator = _simulatorPool.Acquire(width, height);
            await _waitAsync(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
        else if (_simulatorFactory is not null)
        {
            _simulator = _simulatorFactory();
            _simulator.Initialize(width, height);
            await _waitAsync(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("No input simulator pool or factory provided.");
        }

        Volatile.Write(ref _released, 0);
    }

    public void AttachInputState(IEventExecutor? executor, IButtonStateTracker? buttons, IKeyStateTracker? keys)
    {
        _executor = executor;
        _buttons = buttons;
        _keys = keys;
    }

    private IEventExecutor? _executor;
    private IButtonStateTracker? _buttons;
    private IKeyStateTracker? _keys;

    public void Pause()
    {
        if (!IsPlaying || _isPaused)
        {
            return;
        }

        _isPaused = true;
        _pausedButtons = _buttons?.PressedButtons is { } buttons ? new List<ushort>(buttons).ToArray() : Array.Empty<ushort>();
        _pausedKeys = _keys?.PressedKeys is { } keys ? new List<int>(keys).ToArray() : Array.Empty<int>();
        _executor?.ReleaseAll();
        _pauseEvent.Reset();
    }

    public void Resume()
    {
        if (!IsPlaying || !_isPaused)
        {
            return;
        }

        if (_simulator is not null)
        {
            _buttons?.RestoreAll(_simulator, _pausedButtons);
            if (_keys is not null)
            {
                var modifiers = new List<int>();
                foreach (var key in _pausedKeys)
                {
                    if (key is InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL
                        or InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT
                        or InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT
                        or InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA)
                    {
                        modifiers.Add(key);
                    }
                }

                _keys.RestoreAll(_simulator, modifiers);
            }
        }

        _pausedButtons = Array.Empty<ushort>();
        _pausedKeys = Array.Empty<int>();
        _isPaused = false;
        Interlocked.Increment(ref _pauseVersion);
        _pauseEvent.Set();
    }

    public void Stop()
    {
        _executor?.ReleaseAll();
        _pauseEvent.Set();
        _cancellation?.Cancel();
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isPaused)
        {
            await Task.Run(() => _pauseEvent.Wait(cancellationToken), cancellationToken);
        }
    }

    public void End()
    {
        _executor?.ReleaseAll();
        _executor = null;
        ReleaseSimulator();
        _cancellation?.Dispose();
        _cancellation = null;
        IsPlaying = false;
        _isPaused = false;
    }

    private void ReleaseSimulator()
    {
        var simulator = _simulator;
        if (simulator is null || Interlocked.Exchange(ref _released, 1) is not 0)
        {
            return;
        }

        _simulator = null;
        if (_simulatorPool is not null)
        {
            _simulatorPool.Release(simulator, _width, _height);
        }
        else
        {
            simulator.Dispose();
        }

        _width = 0;
        _height = 0;
    }

    public void Dispose()
    {
        Stop();
        End();
        _pauseEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
