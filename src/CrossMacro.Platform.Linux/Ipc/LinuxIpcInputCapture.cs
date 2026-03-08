using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Ipc;

public class LinuxIpcInputCapture : IInputCapture
{
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

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public LinuxIpcInputCapture(IpcClient client, string? consumerId = null, Func<bool>? isSupportedProbe = null)
    {
        _client = client;
        _isSupportedProbe = isSupportedProbe ?? (() => true);
        _consumerId = string.IsNullOrWhiteSpace(consumerId)
            ? $"linux-ipc-capture-{Interlocked.Increment(ref _captureInstanceSequence)}"
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
            if (startupAttempt.PendingStartTask != null)
            {
                await startupAttempt.PendingStartTask.WaitAsync(ct);
            }

            RegisterStopOnCancellation(ct, throwIfAlreadyCanceled: false);
            return;
        }

        using var startLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            startupAttempt.PendingStartLifetimeCts!.Token);

        try
        {
            await StartCaptureWithReconnectAsync(
                startupAttempt.CaptureMouse,
                startupAttempt.CaptureKeyboard,
                startLifetimeCts.Token);
        }
        catch (OperationCanceledException ex)
        {
            ClearPendingStartupState(ex);
            _client.StopCapture(_consumerId);
            throw;
        }
        catch (Exception ex)
        {
            var startupException = new InvalidOperationException(GetStartupFailureMessage(ex), ex);
            ClearPendingStartupState(startupException);
            _client.StopCapture(_consumerId);
            throw startupException;
        }

        StartupCommit startupCommit;
        lock (_stateLock)
        {
            startupCommit = BuildStartupCommit_NoLock(
                startupAttempt.PendingStartLifetimeCts,
                ct,
                startupAttempt.StartupConfigurationVersion);
        }

        startupCommit.StartupStateToDispose?.Dispose();

        try
        {
            if (startupCommit.ShouldStopImmediately)
            {
                _client.StopCapture(_consumerId);
                throw new OperationCanceledException("Capture startup was cancelled before completion.");
            }

            if (startupCommit.ShouldApplyDeferredConfiguration)
            {
                ApplyDeferredConfigurationIfCurrent(
                    startupCommit.DeferredConfigurationVersion,
                    startupCommit.DeferredCaptureMouse,
                    startupCommit.DeferredCaptureKeyboard);
            }

            Log.Information("[LinuxIpcInputCapture] Started capture via daemon");
            RegisterStopOnCancellation(ct, throwIfAlreadyCanceled: true);
            _ = startupCommit.StartupCompletion?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _ = startupCommit.StartupCompletion?.TrySetException(ex);
            throw;
        }
    }

    public void Stop()
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

        _stopRegistration.Dispose();
    }

    private void OnClientInputReceived(object? sender, InputCaptureEventArgs e)
    {
        InputReceived?.Invoke(this, e);
    }

    private void OnClientErrorOccurred(object? sender, string error)
    {
        Error?.Invoke(this, error);
    }

    private static string GetStartupFailureMessage(Exception ex)
    {
        if (ex is IpcClientException ipcEx && ipcEx.Reason == IpcClientFailureReason.Timeout)
        {
            return "Timed out while waiting for daemon handshake. Check that crossmacro.service is running and responsive.";
        }

        if (ex is System.IO.IOException ||
            ex.InnerException is System.IO.IOException ||
            ex.InnerException is System.Net.Sockets.SocketException)
        {
            return "Connection rejected by daemon. Polkit authorization was denied or timed out. (System details: " + ex.Message + ")";
        }

        return ex.Message;
    }

    private bool ShouldWaitForReconnect(Exception ex)
    {
        return _client.AutoReconnectEnabled &&
            ex is IpcClientException ipcEx &&
            ipcEx.Reason == IpcClientFailureReason.ConnectFailed;
    }

    private async Task WaitForDaemonReconnectAsync(CancellationToken token)
    {
        while (!_client.IsConnected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), token);
        }
    }

    private void ApplyDeferredConfigurationIfCurrent(
        int expectedConfigurationVersion,
        bool captureMouse,
        bool captureKeyboard)
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

        _client.StartCapture(_consumerId, captureMouse, captureKeyboard);
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

    private async Task StartCaptureWithReconnectAsync(
        bool captureMouse,
        bool captureKeyboard,
        CancellationToken token)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(token);
        }

        while (true)
        {
            try
            {
                await _client.StartCaptureAsync(_consumerId, captureMouse, captureKeyboard, token);
                return;
            }
            catch (Exception ex) when (ShouldWaitForReconnect(ex))
            {
                Log.Warning(
                    ex,
                    "[LinuxIpcInputCapture] Lost daemon connection while waiting for capture start acknowledgement for {ConsumerId}; waiting for reconnect",
                    _consumerId);
                await WaitForDaemonReconnectAsync(token);
            }
        }
    }

    private StartupCommit BuildStartupCommit_NoLock(
        CancellationTokenSource? pendingStartLifetimeCts,
        CancellationToken cancellationToken,
        int startupConfigurationVersion)
    {
        var shouldStopImmediately =
            _disposed ||
            _stopRequestedDuringStartup ||
            pendingStartLifetimeCts?.IsCancellationRequested == true ||
            cancellationToken.IsCancellationRequested;
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

    private void RegisterStopOnCancellation(CancellationToken ct, bool throwIfAlreadyCanceled)
    {
        _stopRegistration.Dispose();
        var stopRegistration = ct.Register(Stop);
        if (throwIfAlreadyCanceled && ct.IsCancellationRequested)
        {
            stopRegistration.Dispose();
            _client.StopCapture(_consumerId);
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
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LinuxIpcInputCapture));
        }
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

        Stop();
        _stopRegistration.Dispose();
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
        catch
        {
            return false;
        }
    }
}
