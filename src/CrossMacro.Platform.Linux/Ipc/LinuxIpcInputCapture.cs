
namespace CrossMacro.Platform.Linux.Ipc;

public sealed class LinuxIpcInputCapture : IInputCapture, IAsyncDisposable
{
    internal readonly record struct StartupFailurePolicy(
        bool WaitForReconnect,
        string UserMessage);

    private readonly record struct StartupAttempt(
        bool ShouldStart,
        Task? PendingStartTask,
        bool CaptureMouse,
        bool CaptureKeyboard,
        int StartupConfigurationVersion,
        CancellationTokenSource? PendingStartLifetimeCts);

    private readonly record struct StartupCommit(
        bool ShouldStopImmediately,
        bool ShouldApplyDeferredConfiguration,
        bool DeferredCaptureMouse,
        bool DeferredCaptureKeyboard,
        int DeferredConfigurationVersion,
        CancellationTokenSource? StartupStateToDispose,
        TaskCompletionSource<bool>? StartupCompletion);

    private static int _captureInstanceSequence;
    private readonly IpcClient _client;
    private readonly Func<bool> _isSupportedProbe;
    private readonly string _consumerId;
    private readonly Lock _stateLock = new();
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;
    private int _configurationVersion;
    private bool _started;
    private bool _startPending;
    private bool _stopRequestedDuringStartup;
    private bool _disposed;
    private CancellationTokenSource? _pendingStartLifetimeCts;
    private TaskCompletionSource<bool>? _pendingStartCompletion;
    private CancellationTokenRegistration _stopRegistration;

    public string ProviderName => "Secure Daemon (Evdev)";

    public bool IsSupported => !_disposed && (_client.IsConnected || IsProbeSupported());

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public LinuxIpcInputCapture(IpcClient client, string? consumerId = null, Func<bool>? isSupportedProbe = null)
    {
        _client = client;
        _isSupportedProbe = isSupportedProbe ?? (static () => true);
        _consumerId = string.IsNullOrWhiteSpace(consumerId)
            ? $"linux-ipc-capture-{Interlocked.Increment(ref _captureInstanceSequence).ToString(CultureInfo.InvariantCulture)}"
            : consumerId;

        _client.InputReceived += OnClientInputReceived;
        _client.ErrorOccurred += OnClientErrorOccurred;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        bool needsUpdate;

        lock (_stateLock)
        {
            ThrowIfDisposed();

            var configurationChanged = _captureMouse != captureMouse || _captureKeyboard != captureKeyboard;
            _captureMouse = captureMouse;
            _captureKeyboard = captureKeyboard;

            if (configurationChanged)
            {
                _configurationVersion++;
            }

            needsUpdate = _started && configurationChanged;
        }

        if (needsUpdate)
        {
            _client.StartCapture(_consumerId, captureMouse, captureKeyboard);
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        var startupAttempt = BeginStartupAttempt();
        if (!startupAttempt.ShouldStart)
        {
            if (startupAttempt.PendingStartTask is not null)
            {
                await startupAttempt.PendingStartTask.WaitAsync(ct).ConfigureAwait(false);
            }

            await RegisterStopOnCancellationAsync(throwIfAlreadyCanceled: false, ct).ConfigureAwait(false);
            return;
        }

        using var startLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            startupAttempt.PendingStartLifetimeCts!.Token);

        try
        {
            await StartCaptureWithStartupPolicyAsync(
                startupAttempt.CaptureMouse,
                startupAttempt.CaptureKeyboard,
                startLifetimeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            ClearPendingStartupState(ex);
            // Rollback must not use the already-cancelled ct, or daemon-side capture stays on.
            await _client.StopCaptureAsync(_consumerId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var startupException = ex is InvalidOperationException ioe
                ? ioe
                : new InvalidOperationException(GetStartupFailureMessage(ex), ex);
            ClearPendingStartupState(startupException);
            await _client.StopCaptureAsync(_consumerId, CancellationToken.None).ConfigureAwait(false);
            throw startupException;
        }

        StartupCommit startupCommit;
        lock (_stateLock)
        {
            startupCommit = BuildStartupCommit_NoLock(
                startupAttempt.PendingStartLifetimeCts,
                startupAttempt.StartupConfigurationVersion,
                ct);
        }

        startupCommit.StartupStateToDispose?.Dispose();

        await CommitStartupAsync(startupCommit, ct).ConfigureAwait(false);
    }

    private async Task CommitStartupAsync(StartupCommit startupCommit, CancellationToken ct)
    {
        try
        {
            if (startupCommit.ShouldStopImmediately)
            {
                await _client.StopCaptureAsync(_consumerId, ct).ConfigureAwait(false);
                throw new OperationCanceledException("Capture startup was cancelled before completion.");
            }

            if (startupCommit.ShouldApplyDeferredConfiguration)
            {
                await ApplyDeferredConfigurationIfCurrentAsync(
                    startupCommit.DeferredConfigurationVersion,
                    startupCommit.DeferredCaptureMouse,
                    startupCommit.DeferredCaptureKeyboard,
                    ct).ConfigureAwait(false);
            }

            Log.Information("[LinuxIpcInputCapture] Started capture via daemon (ConsumerId={ConsumerId})", _consumerId);
            await RegisterStopOnCancellationAsync(throwIfAlreadyCanceled: true, ct).ConfigureAwait(false);
            _ = startupCommit.StartupCompletion?.TrySetResult(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _ = startupCommit.StartupCompletion?.TrySetException(ex);
            throw;
        }
    }

    public void StopCapture()
    {
        bool shouldStopClient = false;
        CancellationTokenSource? pendingStartLifetimeCts = null;

        lock (_stateLock)
        {
            if (_startPending)
            {
                _stopRequestedDuringStartup = true;
                pendingStartLifetimeCts = _pendingStartLifetimeCts;
                shouldStopClient = true;
            }

            if (_started)
            {
                _started = false;
                shouldStopClient = true;
            }
        }

        CancelPendingStartLifetimeSafely(pendingStartLifetimeCts);

        if (shouldStopClient)
        {
            _client.StopCapture(_consumerId);
        }

        var stopRegistration = _stopRegistration;
        _stopRegistration = default;
        stopRegistration.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        bool shouldStopClient = false;
        CancellationTokenSource? pendingStartLifetimeCts = null;

        lock (_stateLock)
        {
            if (_startPending)
            {
                _stopRequestedDuringStartup = true;
                pendingStartLifetimeCts = _pendingStartLifetimeCts;
                shouldStopClient = true;
            }

            if (_started)
            {
                _started = false;
                shouldStopClient = true;
            }
        }

        await CancelPendingStartLifetimeSafelyAsync(pendingStartLifetimeCts).ConfigureAwait(false);

        if (shouldStopClient)
        {
            await _client.StopCaptureAsync(_consumerId, CancellationToken.None).ConfigureAwait(false);
        }

        var stopRegistration = _stopRegistration;
        _stopRegistration = default;
        await stopRegistration.DisposeAsync().ConfigureAwait(false);

        _pendingStartLifetimeCts?.Dispose();
        _client.InputReceived -= OnClientInputReceived;
        _client.ErrorOccurred -= OnClientErrorOccurred;
        GC.SuppressFinalize(this);
    }

    private void OnClientInputReceived(object? sender, CapturedInputEventArgs e)
    {
        InputReceived?.Invoke(this, e);
    }

    private void OnClientErrorOccurred(object? sender, InputCaptureErrorEventArgs error)
    {
        CaptureError?.Invoke(this, error);
    }

    internal static string GetStartupFailureMessage(Exception ex)
    {
        if (ex is not IpcClientException ipcEx)
        {
            return ex.Message;
        }

        return ipcEx.Reason switch
        {
            IpcClientFailureReason.Timeout =>
                "Timed out while waiting for daemon handshake. Check that crossmacro.service is running and responsive.",
            IpcClientFailureReason.SocketNotFound =>
                "CrossMacro daemon is not reachable (the service is stopped or restarting). The connection will be retried automatically.",
            IpcClientFailureReason.PermissionDenied =>
                "Permission denied while accessing the daemon socket. Check that your user is in the 'crossmacro' group.",
            // The daemon supplies its own explanation here (protocol mismatch, uinput init, etc.).
            IpcClientFailureReason.HandshakeFailed =>
                $"Connection rejected by daemon. {ipcEx.Message}",
            IpcClientFailureReason.ProtocolMismatch =>
                ipcEx.Message,
            IpcClientFailureReason.ConnectFailed =>
                ipcEx.InnerException is not null
                    ? $"{ipcEx.Message} (System details: {ipcEx.InnerException.Message})"
                    : ipcEx.Message,
            IpcClientFailureReason.SimulationRejected =>
                $"The daemon rejected an input simulation request. {ipcEx.Message}",
            IpcClientFailureReason.IntegrityMismatch =>
                $"Input delivery integrity verification failed. {ipcEx.Message}",
            // Forward compatibility: unknown reasons fall back to the raw message.
            _ => ex.Message,
        };
    }

    internal StartupFailurePolicy ClassifyCaptureStartupFailure(Exception ex)
    {
        var userMessage = GetStartupFailureMessage(ex);
        var shouldWaitForReconnect = _client.AutoReconnectEnabled
            && ex is IpcClientException ipcEx
            && ipcEx.Reason is IpcClientFailureReason.ConnectFailed or IpcClientFailureReason.SocketNotFound;

        return new StartupFailurePolicy(shouldWaitForReconnect, userMessage);
    }

    private async Task WaitForDaemonReconnectAsync(CancellationToken token)
    {
        while (!_client.IsConnected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token).ConfigureAwait(false);
        }
    }

    private async Task ApplyDeferredConfigurationIfCurrentAsync(
        int expectedConfigurationVersion,
        bool captureMouse,
        bool captureKeyboard,
        CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_disposed ||
                !_started ||
                _startPending ||
                _configurationVersion != expectedConfigurationVersion)
            {
                return;
            }
        }

        await _client.StartCaptureAsync(_consumerId, captureMouse, captureKeyboard, cancellationToken).ConfigureAwait(false);
    }

    private StartupAttempt BeginStartupAttempt()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            var captureMouse = _captureMouse;
            var captureKeyboard = _captureKeyboard;
            var startupConfigurationVersion = _configurationVersion;

            if (_started)
            {
                return new StartupAttempt(
                    ShouldStart: false,
                    PendingStartTask: null,
                    CaptureMouse: captureMouse,
                    CaptureKeyboard: captureKeyboard,
                    StartupConfigurationVersion: startupConfigurationVersion,
                    PendingStartLifetimeCts: null);
            }

            if (_startPending)
            {
                return new StartupAttempt(
                    ShouldStart: false,
                    PendingStartTask: _pendingStartCompletion?.Task ?? Task.CompletedTask,
                    CaptureMouse: captureMouse,
                    CaptureKeyboard: captureKeyboard,
                    StartupConfigurationVersion: startupConfigurationVersion,
                    PendingStartLifetimeCts: null);
            }

            _startPending = true;
            _stopRequestedDuringStartup = false;
            _pendingStartLifetimeCts?.Dispose();
            _pendingStartLifetimeCts = new CancellationTokenSource();
            _pendingStartCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            return new StartupAttempt(
                ShouldStart: true,
                PendingStartTask: null,
                CaptureMouse: captureMouse,
                CaptureKeyboard: captureKeyboard,
                StartupConfigurationVersion: startupConfigurationVersion,
                PendingStartLifetimeCts: _pendingStartLifetimeCts);
        }
    }

    private async Task StartCaptureWithStartupPolicyAsync(
        bool captureMouse,
        bool captureKeyboard,
        CancellationToken token)
    {
        if (!_client.IsConnected)
        {
            try
            {
                await _client.ConnectAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw new InvalidOperationException(GetStartupFailureMessage(ex), ex);
            }
        }

        while (true)
        {
            try
            {
                await _client.StartCaptureAsync(_consumerId, captureMouse, captureKeyboard, token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                var startupFailure = ClassifyCaptureStartupFailure(ex);
                if (!startupFailure.WaitForReconnect)
                {
                    throw new InvalidOperationException(startupFailure.UserMessage, ex);
                }

                Log.Warning(
                    ex,
                    "[LinuxIpcInputCapture] Lost daemon connection while waiting for capture start acknowledgement for {ConsumerId}; waiting for reconnect",
                    _consumerId);
                await WaitForDaemonReconnectAsync(token).ConfigureAwait(false);
            }
        }
    }

    private StartupCommit BuildStartupCommit_NoLock(
        CancellationTokenSource? pendingStartLifetimeCts,
        int startupConfigurationVersion,
        CancellationToken cancellationToken)
    {
        var shouldStopImmediately =
            _disposed || _stopRequestedDuringStartup || (pendingStartLifetimeCts?.IsCancellationRequested) is true || cancellationToken.IsCancellationRequested;
        var deferredCaptureMouse = _captureMouse;
        var deferredCaptureKeyboard = _captureKeyboard;
        var deferredConfigurationVersion = _configurationVersion;
        var shouldApplyDeferredConfiguration =
            !shouldStopImmediately &&
            deferredConfigurationVersion != startupConfigurationVersion;
        var (startupStateToDispose, startupCompletion) = ResetPendingStartupState_NoLock();

        if (!shouldStopImmediately)
        {
            _started = true;
        }

        return new StartupCommit(
            ShouldStopImmediately: shouldStopImmediately,
            ShouldApplyDeferredConfiguration: shouldApplyDeferredConfiguration,
            DeferredCaptureMouse: deferredCaptureMouse,
            DeferredCaptureKeyboard: deferredCaptureKeyboard,
            DeferredConfigurationVersion: deferredConfigurationVersion,
            StartupStateToDispose: startupStateToDispose,
            StartupCompletion: startupCompletion);
    }

    private async Task RegisterStopOnCancellationAsync(bool throwIfAlreadyCanceled, CancellationToken ct)
    {
        var previousStopRegistration = _stopRegistration;
        _stopRegistration = default;
        await previousStopRegistration.DisposeAsync().ConfigureAwait(false);

            var stopRegistration = ct.Register(static state =>
            {
                var capture = (LinuxIpcInputCapture)state!;
                _ = capture._client.StopCaptureAsync(capture._consumerId, CancellationToken.None);
            }, this);
            if (throwIfAlreadyCanceled && ct.IsCancellationRequested)
            {
                await stopRegistration.DisposeAsync().ConfigureAwait(false);
                await _client.StopCaptureAsync(_consumerId, CancellationToken.None).ConfigureAwait(false);
                throw new OperationCanceledException("Capture startup was cancelled before completion.", ct);
            }

        _stopRegistration = stopRegistration;
    }

    private static void CancelPendingStartLifetimeSafely(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // expected when CTS was already disposed concurrently during shutdown.
        }
    }

    private static async ValueTask CancelPendingStartLifetimeSafelyAsync(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // expected when CTS was already disposed concurrently during shutdown.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        StopCapture();
        _pendingStartLifetimeCts?.Dispose();
        _client.InputReceived -= OnClientInputReceived;
        _client.ErrorOccurred -= OnClientErrorOccurred;
        GC.SuppressFinalize(this);
    }

    private void ClearPendingStartupState(Exception exception)
    {
        CancellationTokenSource? startupLifetimeCts;
        TaskCompletionSource<bool>? startupCompletion;
        lock (_stateLock)
        {
            (startupLifetimeCts, startupCompletion) = ResetPendingStartupState_NoLock();
        }

        startupLifetimeCts?.Dispose();
        _ = startupCompletion?.TrySetException(exception);
    }

    private (CancellationTokenSource? StartupLifetimeCts, TaskCompletionSource<bool>? StartupCompletion) ResetPendingStartupState_NoLock()
    {
        var startupLifetimeCts = _pendingStartLifetimeCts;
        var startupCompletion = _pendingStartCompletion;
        _pendingStartLifetimeCts = null;
        _pendingStartCompletion = null;
        _startPending = false;
        _stopRequestedDuringStartup = false;
        return (startupLifetimeCts, startupCompletion);
    }

    private bool IsProbeSupported()
    {
        try
        {
            return _isSupportedProbe();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
