
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns resources whose lifetime is exactly one playback session. Pause, resume, stop and end
/// transitions are serialized by one lock; the pause flag and its completion source are published
/// so the playback thread never observes a paused state without a pending completion.
/// </summary>
internal sealed class PlaybackSessionResourceOwner : IDisposable, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly Func<IInputSimulator>? _simulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReadOnlyDictionary<string, string> _variablesView;
    private volatile TaskCompletionSource<object?> _pauseCompletion = CreateCompletedPauseCompletion();
    private volatile bool _isPaused;
    private volatile bool _isPlaying;
    private int _pauseVersion;
    private ushort[] _pausedButtons = [];
    private int[] _pausedKeys = [];
    private CancellationTokenSource? _cancellation;
    private IEventExecutor? _executor;
    private IButtonStateTracker? _buttons;
    private IKeyStateTracker? _keys;
    private int _width;
    private int _height;

    public PlaybackSessionResourceOwner(Func<IInputSimulator>? simulatorFactory, IInputSimulatorPool? simulatorPool)
    {
        _simulatorFactory = simulatorFactory;
        _simulatorPool = simulatorPool;
        _variablesView = new ReadOnlyDictionary<string, string>(_variables);
    }

    public bool IsPlaying => _isPlaying;
    public bool IsPaused => _isPaused;
    public int PauseResumeVersion => Volatile.Read(ref _pauseVersion);
    public IInputSimulator? Simulator { get; private set; }
    public CancellationToken Token => _cancellation?.Token ?? CancellationToken.None;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _variablesView;
    public IDictionary<string, string> Variables => _variables;

    public void Begin(CancellationToken cancellationToken)
    {
        using (_gate.EnterScope())
        {
            if (_isPlaying)
            {
                throw new PlaybackAlreadyInProgressException();
            }

            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _variables.Clear();
            _isPaused = false;
            Volatile.Write(ref _pauseVersion, 0);
            _pauseCompletion = CreateCompletedPauseCompletion();
            _isPlaying = true;
        }
    }

    public async Task AcquireAsync(int width, int height, CancellationToken cancellationToken)
    {
        IInputSimulator simulator;
        if (_simulatorPool is not null)
        {
            simulator = await _simulatorPool.AcquireAsync(width, height, cancellationToken).ConfigureAwait(false);
        }
        else if (_simulatorFactory is not null)
        {
            simulator = _simulatorFactory();
            await simulator.InitializeAsync(width, height, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException("No input simulator pool or factory provided.");
        }

        using (_gate.EnterScope())
        {
            _width = width;
            _height = height;
            Simulator = simulator;
        }
    }

    public void AttachInputState(IEventExecutor? executor, IButtonStateTracker? buttons, IKeyStateTracker? keys)
    {
        using (_gate.EnterScope())
        {
            _executor = executor;
            _buttons = buttons;
            _keys = keys;
        }
    }

    /// <summary>Pauses playback and releases held input; reports how many inputs were saved for resume.</summary>
    public bool TryPause(out int savedButtonCount, out int savedKeyCount)
    {
        using (_gate.EnterScope())
        {
            if (!_isPlaying || _isPaused)
            {
                savedButtonCount = 0;
                savedKeyCount = 0;
                return false;
            }

            // The completion must exist before the paused flag becomes visible.
            _pauseCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _isPaused = true;
            _pausedButtons = _buttons is null ? [] : [.. _buttons.PressedButtons];
            _pausedKeys = _keys is null ? [] : [.. _keys.PressedKeys];
            savedButtonCount = _pausedButtons.Length;
            savedKeyCount = _pausedKeys.Length;
            _executor?.ReleaseAll();
            return true;
        }
    }

    public bool TryResume()
    {
        using (_gate.EnterScope())
        {
            if (!_isPlaying || !_isPaused)
            {
                return false;
            }

            if (Simulator is not null)
            {
                _buttons?.RestoreAll(Simulator, _pausedButtons);
                _keys?.RestoreAll(Simulator, _pausedKeys.Where(InputEventCode.IsModifierKey));
            }

            _pausedButtons = [];
            _pausedKeys = [];
            _isPaused = false;
            _ = Interlocked.Increment(ref _pauseVersion);
            _ = _pauseCompletion.TrySetResult(null);
            return true;
        }
    }

    public void StopPlayback()
    {
        using (_gate.EnterScope())
        {
            _executor?.ReleaseAll();
            _ = _pauseCompletion.TrySetResult(null);
            _cancellation?.Cancel();
        }
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isPaused)
        {
            _ = await _pauseCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void End()
    {
        using (_gate.EnterScope())
        {
            _executor?.ReleaseAll();
            _executor = null;
            _buttons = null;
            _keys = null;
            ReleaseSimulator();
            _ = _pauseCompletion.TrySetResult(null);
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _isPlaying = false;
            _isPaused = false;
        }
    }

    private void ReleaseSimulator()
    {
        var simulator = Simulator;
        if (simulator is null)
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

    public void Dispose() => End();

    private static TaskCompletionSource<object?> CreateCompletedPauseCompletion()
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = completion.TrySetResult(null);
        return completion;
    }
}
