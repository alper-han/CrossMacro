
namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Client of the daemon IPC stack. Owns the public API, events and disposal orchestration;
/// delegates connection lifecycle to <see cref="IpcTransport"/>, capture-session state to
/// <see cref="IpcCaptureController"/> and simulation traffic to <see cref="IpcSimulationChannel"/>.
/// </summary>
public sealed class IpcClient : IDisposable, IAsyncDisposable
{
    private const string DefaultConsumerId = "default";

    private readonly IpcTransport _transport;
    private readonly IpcCaptureController _capture;
    private readonly IpcSimulationChannel _simulation;
    private readonly Lock _disposeLock = new();
    private Task? _disposeTask;
    private TaskCompletionSource<bool> _transportConnected = CreateTransportConnectedSignal();

    public IpcClient(Func<string>? socketPathResolver = null, bool autoReconnect = true)
    {
        _transport = new IpcTransport(
            socketPathResolver ?? ResolveSocketPath,
            autoReconnect,
            CreateTransportHandlers(),
            IpcTransport.DefaultWriteTimeout);
        _capture = new IpcCaptureController(_transport, ThrowIfDisposed, RaiseErrorOccurredSafely, RaiseErrorOccurredDeferred);
        _simulation = new IpcSimulationChannel(_transport);
    }

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? ErrorOccurred;

    public bool IsConnected => _transport.IsConnected;
    internal bool AutoReconnectEnabled => _transport.AutoReconnectEnabled;

    /// <summary>
    /// Completes once a session is usable: connected and its capture command replay finished. Waiting on the
    /// raw connection state instead would let a retry race the replay and issue the same command twice.
    /// </summary>
    internal async Task WaitForTransportConnectionAsync(CancellationToken cancellationToken)
    {
        bool connected;
        do
        {
            connected = await Volatile.Read(ref _transportConnected).Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        while (!connected);
    }

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
            IpcMessageCodec.WriteResolutionPayload(w, width, height);
        }, throwOnFailure: true);
    }

    public void Cleanup() => _transport.Cleanup(clearSubscriptions: true, disableReconnect: true);

    private IpcTransportHandlers CreateTransportHandlers() => new(
        OnMessage,
        token => _capture.ReplayAfterConnectAsync(token),
        OnTransportConnected,
        OnTransportDropped,
        OnReadLoopFailure,
        (opcode, exception) => _capture.OnSendFailure(opcode, exception),
        clearSubscriptions => _capture.OnCleanupSubscriptions(clearSubscriptions));

    private void OnMessage(BinaryReader reader, IpcOpCode opcode)
    {
        switch (opcode)
        {
            case IpcOpCode.InputEvent:
                DispatchInputEvent(reader);
                break;

            case IpcOpCode.CaptureStarted:
                _capture.HandleCaptureStartedMessage(IpcMessageCodec.ReadRequestId(reader));
                break;

            case IpcOpCode.CaptureStartFailed:
                var captureFailure = IpcMessageCodec.ReadRequestFailurePayload(reader);
                _capture.HandleCaptureStartFailedMessage(captureFailure.RequestId, captureFailure.Message);
                break;

            case IpcOpCode.SimulationBatchCompleted:
                var completedBatch = IpcMessageCodec.ReadSimulationBatchCompletedPayload(reader);
                _simulation.HandleBatchCompletedMessage(completedBatch.RequestId, completedBatch.EventCount);
                break;

            case IpcOpCode.SimulationBatchFailed:
                var failedBatch = IpcMessageCodec.ReadRequestFailurePayload(reader);
                _simulation.HandleBatchFailedMessage(failedBatch.RequestId, failedBatch.Message);
                break;

            case IpcOpCode.Error:
                var msg = reader.ReadString();
                Log.Warning("[IpcClient] RX: Error from daemon: {Message}", msg);
                RaiseErrorOccurredSafely(msg);
                break;

            default:
                // Frames carry no length prefix, so an unknown opcode cannot be skipped: every
                // later byte would be misread. Dropping the session lets the transport reconnect.
                throw new InvalidDataException(
                    $"Daemon sent unknown IPC opcode {((byte)opcode).ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private void OnTransportConnected()
        => Volatile.Read(ref _transportConnected).TrySetResult(true);

    private void OnTransportDropped(bool deferErrorNotifications)
    {
        var previousSignal = Interlocked.Exchange(
            ref _transportConnected,
            CreateTransportConnectedSignal());
        _ = previousSignal.TrySetResult(false);

        _simulation.FailAllPending(new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Daemon connection was lost while waiting for simulation batch acknowledgement."));

        _capture.OnTransportDropped(deferErrorNotifications);
    }

    private void OnReadLoopFailure(Exception exception)
    {
        _capture.OnReadLoopFailure(exception);
        _simulation.FailAllPending(new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Daemon connection was lost while waiting for simulation batch acknowledgement.",
            exception));
    }

    private void DispatchInputEvent(BinaryReader reader)
    {
        var wireEvent = IpcMessageCodec.ReadInputEventPayload(reader);
        var type = (InputEventType)wireEvent.Type;
        var code = wireEvent.Code;
        var value = wireEvent.Value;
        var timestampMicroseconds = wireEvent.Timestamp;

        Log.Debug("[IpcClient] RX: InputEvent Type={Type} Code={Code} Value={Value}", type, code, value);

        InputReceived?.Invoke(this, new CapturedInputEventArgs(new CapturedInputEvent
        {
            Type = type,
            Code = code,
            Value = value,
            TimestampMicroseconds = timestampMicroseconds,
            DeviceName = "Daemon Device",
        }));
    }

    private void RaiseErrorOccurredDeferred(string message)
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

        _capture.PublishAfterCommands(() =>
            InvokeErrorOccurredHandlersSafely(handler, message, "deferred notification"));
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
            if (_disposeTask is null)
            {
                _transport.MarkDisposed();
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
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

    private static TaskCompletionSource<bool> CreateTransportConnectedSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        catch (IOException ex)
        {
            // FileNotFound/DirectoryNotFound from the probe mean the daemon is down
            // (it unlinks the socket on stop and recreates it on start).
            throw new IpcClientException(
                IpcClientFailureReason.SocketNotFound,
                SocketNotFoundMessage(),
                ex);
        }

        throw new IpcClientException(
            IpcClientFailureReason.SocketNotFound,
            SocketNotFoundMessage());
    }

    private static string SocketNotFoundMessage()
    {
        return "Daemon socket not found. Checked:\n" +
            $"  - {IpcProtocol.DefaultSocketPath}\n" +
            "Is the CrossMacro daemon service running?";
    }

    private static void ProbeSocketPathAccess(string socketPath)
    {
        _ = File.GetAttributes(socketPath);
    }
}
