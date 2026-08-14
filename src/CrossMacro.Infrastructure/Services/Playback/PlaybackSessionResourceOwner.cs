
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns resources whose lifetime is exactly one playback session.
/// </summary>
internal sealed class PlaybackSessionResourceOwner(
    Func<IInputSimulator>? simulatorFactory,
    IInputSimulatorPool? simulatorPool) : IDisposable, IAsyncDisposable, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly Func<IInputSimulator>? _simulatorFactory = simulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool = simulatorPool;
    private TaskCompletionSource<object?> _pauseCompletion = CreateCompletedPauseCompletion();
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private int _released;
    private int _pauseVersion;
    private ushort[] _pausedButtons = [];
    private int[] _pausedKeys = [];
    private CancellationTokenSource? _cancellation;
    private int _width;
    private int _height;

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public int PauseResumeVersion => Volatile.Read(ref _pauseVersion);
    public IInputSimulator? Simulator { get; private set; }
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
        IsPaused = false;
        Volatile.Write(ref _pauseVersion, 0);
        _pauseCompletion = CreateCompletedPauseCompletion();
        IsPlaying = true;
    }

    public async Task AcquireAsync(int width, int height, CancellationToken cancellationToken)
    {
        _width = width;
        _height = height;
        if (_simulatorPool is not null)
        {
            Simulator = await _simulatorPool.AcquireAsync(width, height, cancellationToken).ConfigureAwait(false);
        }
        else if (_simulatorFactory is not null)
        {
            Simulator = _simulatorFactory();
            await Simulator.InitializeAsync(width, height, cancellationToken).ConfigureAwait(false);
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
        if (!IsPlaying || IsPaused)
        {
            return;
        }

        IsPaused = true;
        _pausedButtons = _buttons?.PressedButtons is { } buttons ? new List<ushort>(buttons).ToArray() : [];
        _pausedKeys = _keys?.PressedKeys is { } keys ? new List<int>(keys).ToArray() : [];
        _executor?.ReleaseAll();
        _pauseCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void ResumePlayback()
    {
        if (!IsPlaying || !IsPaused)
        {
            return;
        }

        if (Simulator is not null)
        {
            _buttons?.RestoreAll(Simulator, _pausedButtons);
            if (_keys is not null)
            {
                var modifiers = _pausedKeys
                    .Where(key => key is InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL
                        or InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT
                        or InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT
                        or InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA)
                    .ToList();

                _keys.RestoreAll(Simulator, modifiers);
            }
        }

        _pausedButtons = [];
        _pausedKeys = [];
        IsPaused = false;
        _ = Interlocked.Increment(ref _pauseVersion);
        _ = _pauseCompletion.TrySetResult(null);
    }

    public void StopPlayback()
    {
        _executor?.ReleaseAll();
        _ = _pauseCompletion.TrySetResult(null);
        _cancellation?.Cancel();
    }

    public async ValueTask StopPlaybackAsync()
    {
        _executor?.ReleaseAll();
        _ = _pauseCompletion.TrySetResult(null);
        if (_cancellation is not null)
        {
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (IsPaused)
        {
            _ = await _pauseCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        IsPaused = false;
    }

    private void ReleaseSimulator()
    {
        var simulator = Simulator;
        if (simulator is null || Interlocked.Exchange(ref _released, 1) is not 0)
        {
            return;
        }

        Simulator = null;
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
        StopPlayback();
        End();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await StopPlaybackAsync().ConfigureAwait(false);
        End();
        GC.SuppressFinalize(this);
    }

    private static TaskCompletionSource<object?> CreateCompletedPauseCompletion()
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = completion.TrySetResult(null);
        return completion;
    }
}
