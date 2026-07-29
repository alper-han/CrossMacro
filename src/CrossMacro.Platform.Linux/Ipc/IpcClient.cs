
namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Facade over the daemon IPC stack. Owns the public API, events and disposal orchestration;
/// delegates connection lifecycle to <see cref="IpcTransport"/>, capture-session state to
/// <see cref="IpcCaptureController"/> and simulation traffic to <see cref="IpcSimulationChannel"/>.
/// </summary>
public sealed class IpcClient : IDisposable, IAsyncDisposable, IIpcTransportCallbacks
{
    private const string DefaultConsumerId = "default";

    private readonly IpcTransport _transport;
    private readonly IpcCaptureController _capture;
    private readonly IpcSimulationChannel _simulation;
    private readonly Lock _disposeLock = new();

    public IpcClient(Func<string>? socketPathResolver = null, bool autoReconnect = true)
    {
        _transport = new IpcTransport(socketPathResolver ?? ResolveSocketPath, autoReconnect, this, this);
        _capture = new IpcCaptureController(_transport, ThrowIfDisposed, RaiseErrorOccurredSafely, RaiseErrorOccurredDeferred);
        _simulation = new IpcSimulationChannel(_transport);
    }

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? ErrorOccurred;

    public bool IsConnected => _transport.IsConnected;
    internal bool AutoReconnectEnabled => _transport.AutoReconnectEnabled;

    // Internal seams so tests exercise gate ordering without reflection.
    internal SemaphoreSlim WriteGate => _transport.WriteGate;
    internal SemaphoreSlim ConnectGate => _transport.ConnectGate;
    internal SemaphoreSlim CaptureCommandGate => _capture.CommandGate;
    internal PendingCaptureStartRegistry PendingCaptureStarts => _capture.PendingCaptureStarts;
    internal Task? DisposeTask { get; private set; }

    internal void HandleSendFailureForSession(Exception ex, IpcOpCode op, bool throwOnFailure, int? sessionGeneration)
        => _transport.HandleSendFailure(ex, op, throwOnFailure, sessionGeneration);

    internal Task StartDeferredCaptureReconcileAsync()
        => _capture.StartDeferredCaptureReconcileAsync();

    public Task ConnectAsync(CancellationToken token) => _transport.ConnectAsync(token);

    public void StartCapture(bool mouse, bool keyboard) => _capture.StartCapture(DefaultConsumerId, mouse, keyboard);

    public void StartCapture(string consumerId, bool mouse, bool keyboard) => _capture.StartCapture(consumerId, mouse, keyboard);

    public Task StartCaptureAsync(bool mouse, bool keyboard, CancellationToken token = default)
        => _capture.StartCaptureAsync(DefaultConsumerId, mouse, keyboard, token);

    public Task StartCaptureAsync(string consumerId, bool mouse, bool keyboard, CancellationToken token = default)
        => _capture.StartCaptureAsync(consumerId, mouse, keyboard, token);

    public void StopCapture() => _capture.StopCapture(DefaultConsumerId);

    public void StopCapture(string consumerId) => _capture.StopCapture(consumerId);

    public Task StopCaptureAsync() => _capture.StopCaptureAsync(DefaultConsumerId, CancellationToken.None);

    public Task StopCaptureAsync(string consumerId, CancellationToken token = default)
        => _capture.StopCaptureAsync(consumerId, token);

    public void SimulateEvent(ushort type, ushort code, int value) => _simulation.SimulateEvent(type, code, value);

    public void SimulateEvents(ReadOnlySpan<(ushort Type, ushort Code, int Value)> events) => _simulation.SimulateEvents(events);

    public void SimulateEventBatch(ReadOnlySpan<InputSimulationStep> steps) => _simulation.SimulateEventBatch(steps);

    public Task SimulateEventBatchAsync(IReadOnlyList<InputSimulationStep> steps, CancellationToken cancellationToken = default)
        => _simulation.SimulateEventBatchAsync(steps, cancellationToken);

    public void ConfigureResolution(int width, int height)
    {
        _ = _transport.Send(IpcOpCode.ConfigureResolution, w =>
        {
            w.Write(width);
            w.Write(height);
        }, throwOnFailure: true);
    }

    public void Cleanup() => _transport.Cleanup(clearSubscriptions: true, disableReconnect: true);

    void IIpcTransportCallbacks.OnMessage(BinaryReader reader, IpcOpCode opcode)
    {
        switch (opcode)
        {
            case IpcOpCode.InputEvent:
                DispatchInputEvent(reader);
                break;

            case IpcOpCode.CaptureStarted:
                _capture.HandleCaptureStartedMessage(reader.ReadInt32());
                break;

            case IpcOpCode.CaptureStartFailed:
                _capture.HandleCaptureStartFailedMessage(reader.ReadInt32(), reader.ReadString());
                break;

            case IpcOpCode.SimulationBatchCompleted:
                _simulation.HandleBatchCompletedMessage(reader.ReadInt32());
                break;

            case IpcOpCode.SimulationBatchFailed:
                _simulation.HandleBatchFailedMessage(reader.ReadInt32(), reader.ReadString());
                break;

            case IpcOpCode.Error:
                var msg = reader.ReadString();
                Log.Warning("[IpcClient] RX: Error from daemon: {Message}", msg);
                RaiseErrorOccurredSafely(msg);
                break;

            default:
                Log.Warning("[IpcClient] RX: Unknown opcode: {Op}", opcode);
                break;
        }
    }

    Task IIpcTransportCallbacks.ReplayAfterConnectAsync(CancellationToken token)
        => _capture.ReplayAfterConnectAsync(token);

    void IIpcTransportCallbacks.OnTransportDropped(bool deferErrorNotifications)
    {
        _simulation.FailAllPending(new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Daemon connection was lost while waiting for simulation batch acknowledgement."));

        _capture.OnTransportDropped(deferErrorNotifications);
    }

    void IIpcTransportCallbacks.OnReadLoopFailure(Exception exception)
    {
        _capture.OnReadLoopFailure(exception);
        _simulation.FailAllPending(new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Daemon connection was lost while waiting for simulation batch acknowledgement.",
            exception));
    }

    void IIpcTransportCallbacks.OnSendFailure(IpcOpCode opcode, Exception exception)
        => _capture.OnSendFailure(opcode, exception);

    void IIpcTransportCallbacks.OnCleanupSubscriptions(bool clearSubscriptions)
        => _capture.OnCleanupSubscriptions(clearSubscriptions);

    private void DispatchInputEvent(BinaryReader reader)
    {
        var type = (InputEventType)reader.ReadByte();
        var code = reader.ReadInt32();
        var value = reader.ReadInt32();
        var timestamp = reader.ReadInt64();

        Log.Debug("[IpcClient] RX: InputEvent Type={Type} Code={Code} Value={Value}", type, code, value);

        InputReceived?.Invoke(this, new CapturedInputEventArgs(new CapturedInputEvent
        {
            Type = type,
            Code = code,
            Value = value,
            Timestamp = timestamp,
            DeviceName = "Daemon Device",
        }));
    }

    internal void RaiseErrorOccurredDeferred(string message)
    {
        if (_transport.IsDisposed)
        {
            return;
        }

        var handler = ErrorOccurred;
        if (handler is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            if (_transport.IsDisposed)
            {
                return;
            }

            var gateAcquired = false;
            try
            {
                // Ensure callbacks are dispatched only after any in-flight capture command
                // exits its gate, avoiding re-entrant waits on the same gate.
                await _capture.EnterCommandGateAsync(CancellationToken.None).ConfigureAwait(false);
                gateAcquired = true;
            }
            catch (ObjectDisposedException)
            {
                // The client is being torn down; the pending event has nowhere to
                // go, and finally will not release the gate since gateAcquired is
                // still false at this point.
            }
            finally
            {
                if (gateAcquired)
                {
                    _capture.ExitCommandGate();
                }
            }

            if (!_transport.IsDisposed)
            {
                InvokeErrorOccurredHandlersSafely(handler, message, "deferred notification");
            }
        }, CancellationToken.None);
    }

    private void RaiseErrorOccurredSafely(string message)
    {
        var handler = ErrorOccurred;
        if (handler is null)
        {
            return;
        }

        InvokeErrorOccurredHandlersSafely(handler, message, "notification");
    }

    private void InvokeErrorOccurredHandlersSafely(
        EventHandler<InputCaptureErrorEventArgs> handlers,
        string message,
        string notificationContext)
    {
        var args = new InputCaptureErrorEventArgs(message);
        foreach (var d in handlers.GetInvocationList())
        {
            if (d is not EventHandler<InputCaptureErrorEventArgs> handler)
            {
                continue;
            }
            try
            {
                handler(this, args);
            }
            catch (Exception callbackError) when (callbackError is not OutOfMemoryException)
            {
                Log.Warning(
                    callbackError,
                    "[IpcClient] Error callback threw during {NotificationContext}",
                    notificationContext);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_transport.IsDisposed, this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (DisposeTask is null)
            {
                _transport.MarkDisposed();
                DisposeTask = DisposeCoreAsync();
            }

            return new ValueTask(DisposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _transport.ShutdownAsync().ConfigureAwait(false);
        await _capture.WaitForDeferredReconcilesAsync().ConfigureAwait(false);

        _capture.Dispose();
        _transport.Dispose();
    }

    private static string ResolveSocketPath()
    {
        return ResolveSocketPath(File.Exists, ProbeSocketPathAccess);
    }

    internal static string ResolveSocketPath(Func<string, bool>? fileExists, Action<string>? probeSocketAccess)
    {
        fileExists ??= File.Exists;
        probeSocketAccess ??= ProbeSocketPathAccess;

        if (fileExists(IpcProtocol.DefaultSocketPath))
        {
            return IpcProtocol.DefaultSocketPath;
        }

        try
        {
            probeSocketAccess(IpcProtocol.DefaultSocketPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IpcClientException(
                IpcClientFailureReason.PermissionDenied,
                $"Daemon socket access denied: {IpcProtocol.DefaultSocketPath}",
                ex);
        }
        catch (IOException ex) when (IpcTransport.IsPermissionDeniedException(ex))
        {
            throw new IpcClientException(
                IpcClientFailureReason.PermissionDenied,
                $"Daemon socket access denied: {IpcProtocol.DefaultSocketPath}",
                ex);
        }

        throw new IpcClientException(
            IpcClientFailureReason.SocketNotFound,
            "Daemon socket not found. Checked:\n" +
            $"  - {IpcProtocol.DefaultSocketPath}\n" +
            "Is the CrossMacro daemon service running?");
    }

    private static void ProbeSocketPathAccess(string socketPath)
    {
        _ = File.GetAttributes(socketPath);
    }
}
