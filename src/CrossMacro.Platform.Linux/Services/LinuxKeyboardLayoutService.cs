
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Linux keyboard layout service - Facade coordinating layout detection, keycode mapping, and XKB state.
/// Implements IKeyboardLayoutService by delegating to specialized components following SRP.
/// </summary>
public sealed class LinuxKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private readonly ILinuxKeyCodeMapper _keyCodeMapper;
    private readonly IXkbStateManager _xkbState;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _lifetimeGate = new();
    private const int InitializationWaitTimeoutMs = 2000;
    private readonly Task _initializationTask;
    private bool _disposed;
    private bool _cancellationRequested;
    private bool _ctsDisposed;

    public LinuxKeyboardLayoutService(
        ILinuxLayoutDetector layoutDetector,
        ILinuxKeyCodeMapper keyCodeMapper,
        IXkbStateManager xkbState)
    {
        ArgumentNullException.ThrowIfNull(layoutDetector);
        _keyCodeMapper = keyCodeMapper;
        _xkbState = xkbState;

        _initializationTask = InitializeAsync(layoutDetector, _cts.Token);
        _ = _initializationTask.ContinueWith(
            static (task, state) => ((LinuxKeyboardLayoutService)state!).DisposeCancellationTokenSource(task),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task InitializeAsync(ILinuxLayoutDetector layoutDetector, CancellationToken cancellationToken)
    {
        try
        {
            var layout = await layoutDetector.DetectLayoutAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            InitializeXkb(layout);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[LinuxKeyboardLayoutService] Layout initialization canceled");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[LinuxKeyboardLayoutService] Failed to initialize XKB state");
            InitializeXkb("us");
        }
    }

    private void InitializeXkb(string? layout)
    {
        lock (_lifetimeGate)
        {
            if (_disposed)
            {
                return;
            }

            _xkbState.Initialize(layout);
        }
    }

    /// <inheritdoc />
    public string GetKeyName(int keyCode)
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();
            return _keyCodeMapper.GetKeyName(keyCode);
        }
    }

    /// <inheritdoc />
    public int GetKeyCode(string keyName)
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();
            return _keyCodeMapper.GetKeyCode(keyName);
        }
    }

    /// <inheritdoc />
    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        ThrowIfDisposedUnderGate();
        EnsureXkbInitialized();
        bool shift = leftShift || rightShift;
        bool altGr = rightAlt;
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();
            return _xkbState.GetCharFromKeyCode(keyCode, shift, altGr, capsLock);
        }
    }

    /// <inheritdoc />
    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        ThrowIfDisposedUnderGate();
        EnsureXkbInitialized();
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();
            return _xkbState.GetInputForChar(c);
        }
    }

    /// <summary>
    /// Briefly waits for background initialization so XKB-dependent queries don't fall back
    /// to the US-default layout.
    /// </summary>
    private void EnsureXkbInitialized()
    {
        if (_initializationTask.IsCompleted)
        {
            return;
        }

        try
        {
            _ = _initializationTask.Wait(InitializationWaitTimeoutMs, CancellationToken.None);
        }
        catch (AggregateException)
        {
            // InitializeAsync logs and swallows its own failures.
        }
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _cts.Cancel();

        lock (_lifetimeGate)
        {
            _cancellationRequested = true;
            _xkbState.Dispose();

            if (_initializationTask.IsCompleted)
            {
                DisposeCancellationTokenSourceUnderGate();
            }
        }
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void DisposeCancellationTokenSource(Task initializationTask)
    {
        _ = initializationTask.Exception;

        lock (_lifetimeGate)
        {
            if (_disposed && _cancellationRequested)
            {
                DisposeCancellationTokenSourceUnderGate();
            }
        }
    }

    private void DisposeCancellationTokenSourceUnderGate()
    {
        if (_ctsDisposed)
        {
            return;
        }

        _cts.Dispose();
        _ctsDisposed = true;
    }

    private void ThrowIfDisposedUnderGate()
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();
        }
    }
}
