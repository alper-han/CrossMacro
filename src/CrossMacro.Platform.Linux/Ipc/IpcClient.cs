using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Ipc;

public class IpcClient : IDisposable
{
    private readonly Func<string> _socketPathResolver;
    private readonly bool _autoReconnect;
    private Socket? _socket;
    private NetworkStream? _stream;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;
    private CancellationTokenSource? _cts;
    private readonly CancellationTokenSource _reconnectCts = new();
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly Lock _reconnectLock = new();
    private Task? _readTask;
    private Task? _reconnectTask;
    private readonly Lock _writeLock = new();
    private readonly CaptureSubscriptionCoordinator _captureCoordinator = new();
    private readonly SemaphoreSlim _captureCommandGate = new(1, 1);
    private readonly PendingCaptureStartRegistry _pendingCaptureStarts = new();
    private bool _disposed;
    private bool _reconnectEnabled = true;
    private const string DefaultConsumerId = "default";
    private const int HandshakeTimeoutMs = 5000;

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _socket?.Connected ?? false;
    internal bool AutoReconnectEnabled => _autoReconnect;

    public IpcClient(Func<string>? socketPathResolver = null, bool autoReconnect = true)
    {
        _socketPathResolver = socketPathResolver ?? ResolveSocketPath;
        _autoReconnect = autoReconnect;
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        ThrowIfDisposed();
        var gateAcquired = false;
        try
        {
            await _connectGate.WaitAsync(token);
            gateAcquired = true;

            if (IsConnected) return;

            var socketPath = _socketPathResolver();

            try
            {
                _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await _socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token);

                // Handshake uses explicit timeout to avoid hanging forever on partial connections.
                _socket.ReceiveTimeout = HandshakeTimeoutMs;
                _socket.SendTimeout = HandshakeTimeoutMs;
                
                _stream = new NetworkStream(_socket);
                _reader = new BinaryReader(_stream);
                _writer = new BinaryWriter(_stream);

                // Handshake
                lock (_writeLock)
                {
                    _writer.Write((byte)IpcOpCode.Handshake);
                    _writer.Write(IpcProtocol.ProtocolVersion);
                    _stream.Flush();
                }

                var opcode = (IpcOpCode)_reader.ReadByte();
                if (opcode == IpcOpCode.Error)
                {
                    var msg = _reader.ReadString();
                    throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {msg}");
                }
                if (opcode != IpcOpCode.Handshake)
                {
                    throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Unexpected handshake opcode: {opcode}");
                }
                var version = _reader.ReadInt32();
                if (version != IpcProtocol.ProtocolVersion)
                {
                    throw new IpcClientException(
                        IpcClientFailureReason.ProtocolMismatch,
                        $"Protocol version mismatch. Daemon: {version}, Client: {IpcProtocol.ProtocolVersion}");
                }

                // Reset to infinite timeout for normal long-running event stream reads.
                _socket.ReceiveTimeout = 0;
                _socket.SendTimeout = 0;

                Log.Information("Connected to CrossMacro Daemon");

                // Start read loop
                _cts = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadLoop(_cts.Token));

                await _captureCommandGate.WaitAsync(token);
                try
                {
                    PendingCaptureStartRegistration? replayPendingStart = null;
                    CaptureCommand replayCommand = default;

                    lock (_captureLock)
                    {
                        _captureCoordinator.ResetTransportState();
                        var command = _captureCoordinator.GetRequiredCommand();
                        if (command.Type != CaptureCommandType.None)
                        {
                            if (command.Type == CaptureCommandType.Start)
                            {
                                var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                                replayPendingStart = _pendingCaptureStarts.Begin(
                                    command,
                                    notifyOnFailure: true,
                                    forceReconcileOnFailure: true,
                                    previousTransportCommand: previousTransportCommand);
                            }

                            _captureCoordinator.MarkCommandIssued(command);
                            replayCommand = command;
                        }
                    }

                    if (replayCommand.Type != CaptureCommandType.None)
                    {
                        try
                        {
                            SendCaptureCommand(
                                replayCommand,
                                requestId: replayPendingStart?.RequestId ?? 0,
                                throwOnFailure: replayCommand.Type == CaptureCommandType.Start);
                        }
                        catch
                        {
                            lock (_captureLock)
                            {
                                _captureCoordinator.MarkTransportStopped();
                            }

                            _pendingCaptureStarts.ClearCurrent(replayPendingStart?.RequestId ?? 0);
                            throw;
                        }
                    }
                }
                finally
                {
                    _captureCommandGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                Cleanup(clearSubscriptions: false, disableReconnect: false);
                throw;
            }
            catch (IpcClientException)
            {
                Cleanup(clearSubscriptions: false, disableReconnect: false);
                throw;
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                Cleanup(clearSubscriptions: false, disableReconnect: false);
                throw new IpcClientException(
                    IpcClientFailureReason.Timeout,
                    "Timed out while connecting to or handshaking with CrossMacro daemon.",
                    ex);
            }
            catch (Exception ex)
            {
                Cleanup(clearSubscriptions: false, disableReconnect: false);
                throw new IpcClientException(IpcClientFailureReason.ConnectFailed, "Failed to connect to daemon.", ex);
            }
        }
        finally
        {
            if (gateAcquired)
            {
                _connectGate.Release();
            }
        }
    }

    private static string ResolveSocketPath()
    {
        if (File.Exists(IpcProtocol.DefaultSocketPath))
        {
            return IpcProtocol.DefaultSocketPath;
        }

        if (File.Exists(IpcProtocol.FallbackSocketPath))
        {
            Log.Information("Using fallback socket path: {Path}", IpcProtocol.FallbackSocketPath);
            return IpcProtocol.FallbackSocketPath;
        }

        throw new IpcClientException(
            IpcClientFailureReason.SocketNotFound,
            $"Daemon socket not found. Checked:\n" +
            $"  - {IpcProtocol.DefaultSocketPath}\n" +
            $"  - {IpcProtocol.FallbackSocketPath}\n" +
            $"Is the CrossMacro daemon service running?");
    }

    private static bool IsTimeoutException(Exception ex)
    {
        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is IOException ioEx &&
            ioEx.InnerException is SocketException ioSocketEx &&
            ioSocketEx.SocketErrorCode == SocketError.TimedOut)
        {
            return true;
        }

        if (ex is SocketException socketEx &&
            socketEx.SocketErrorCode == SocketError.TimedOut)
        {
            return true;
        }

        return false;
    }

    private void ReadLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader != null)
            {
                var opcode = (IpcOpCode)_reader.ReadByte();

                switch (opcode)
                {
                    case IpcOpCode.InputEvent:
                        var type = (InputEventType)_reader.ReadByte();
                        var code = _reader.ReadInt32();
                        var value = _reader.ReadInt32();
                        var timestamp = _reader.ReadInt64();

                        Log.Debug("[IpcClient] RX: InputEvent Type={Type} Code={Code} Value={Value}", type, code, value);

                        InputReceived?.Invoke(this, new InputCaptureEventArgs
                        {
                            Type = type,
                            Code = code,
                            Value = value,
                            Timestamp = timestamp,
                            DeviceName = "Daemon Device"
                        });
                        break;

                    case IpcOpCode.CaptureStarted:
                        HandleCaptureStartedMessage(_reader.ReadInt32());
                        break;

                    case IpcOpCode.CaptureStartFailed:
                        HandleCaptureStartFailedMessage(_reader.ReadInt32(), _reader.ReadString());
                        break;

                    case IpcOpCode.Error:
                        var msg = _reader.ReadString();
                        Log.Warning("[IpcClient] RX: Error from daemon: {Message}", msg);
                        RaiseErrorOccurredSafely(msg);
                        break;

                    default:
                        Log.Warning("[IpcClient] RX: Unknown opcode: {Op}", opcode);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Log.Error(ex, "[IpcClient] Read loop error");
                var failedPendingStart = _pendingCaptureStarts.TryFailCurrent(
                    new IpcClientException(
                        IpcClientFailureReason.ConnectFailed,
                        "Daemon connection was lost during capture startup.",
                        ex),
                    out var notifyOnFailure);
                if (notifyOnFailure || !failedPendingStart)
                {
                    RaiseErrorOccurredSafely("Connection lost: " + ex.Message);
                }
                Cleanup(clearSubscriptions: false, disableReconnect: false);
                StartReconnectLoop();
            }
        }
    }

    private readonly Lock _captureLock = new();

    public void StartCapture(bool mouse, bool keyboard)
    {
        StartCapture(DefaultConsumerId, mouse, keyboard);
    }

    public void StartCapture(string consumerId, bool mouse, bool keyboard)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer id cannot be null or whitespace.", nameof(consumerId));
        }

        CaptureCommand commandToSend = default;
        PendingCaptureStartRegistration? pendingStart = null;
        var shouldSend = false;

        _captureCommandGate.Wait();
        try
        {
            lock (_captureLock)
            {
                _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);

                if (_pendingCaptureStarts.TryGetPendingTask() != null)
                {
                    _pendingCaptureStarts.RequestFailureNotification();
                    return;
                }

                var command = _captureCoordinator.GetRequiredCommand();
                if (command.Type != CaptureCommandType.None)
                {
                    if (command.Type == CaptureCommandType.Start && !IsConnected)
                    {
                        return;
                    }

                        if (command.Type == CaptureCommandType.Start)
                        {
                            var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                            pendingStart = _pendingCaptureStarts.Begin(
                                command,
                                notifyOnFailure: true,
                                forceReconcileOnFailure: true,
                                previousTransportCommand: previousTransportCommand);
                        }

                    _captureCoordinator.MarkCommandIssued(command);
                    commandToSend = command;
                    shouldSend = true;
                }
            }

            if (shouldSend)
            {
                try
                {
                    if (!SendCaptureCommand(commandToSend, requestId: pendingStart?.RequestId ?? 0))
                    {
                        lock (_captureLock)
                        {
                            _captureCoordinator.MarkTransportStopped();
                        }

                        _pendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
                    }
                }
                catch
                {
                    lock (_captureLock)
                    {
                        _captureCoordinator.MarkTransportStopped();
                    }

                    _pendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
                    throw;
                }
            }
        }
        finally
        {
            _captureCommandGate.Release();
        }
    }

    public Task StartCaptureAsync(bool mouse, bool keyboard, CancellationToken token = default)
    {
        return StartCaptureAsync(DefaultConsumerId, mouse, keyboard, token);
    }

    public async Task StartCaptureAsync(string consumerId, bool mouse, bool keyboard, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer id cannot be null or whitespace.", nameof(consumerId));
        }

        ThrowIfDisposed();
        token.ThrowIfCancellationRequested();

        var subscriptionRegistered = false;
        bool hadPreviousSubscription = false;
        bool previousCaptureMouse = false;
        bool previousCaptureKeyboard = false;

        while (true)
        {
            Task? waitTask = null;
            PendingCaptureStartRegistration? createdPendingStart = null;
            CaptureCommand commandToSend = default;
            var joinedExistingPendingStart = false;

            await _captureCommandGate.WaitAsync(token);
            try
            {
                lock (_captureLock)
                {
                    if (!subscriptionRegistered)
                    {
                        hadPreviousSubscription = _captureCoordinator.TryGetSubscription(
                            consumerId,
                            out previousCaptureMouse,
                            out previousCaptureKeyboard);
                        _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);
                        subscriptionRegistered = true;
                    }

                    waitTask = _pendingCaptureStarts.TryGetPendingTask();
                    if (waitTask == null)
                    {
                        var command = _captureCoordinator.GetRequiredCommand();
                        if (command.Type == CaptureCommandType.None)
                        {
                            return;
                        }

                        if (command.Type == CaptureCommandType.Start)
                        {
                            var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                            createdPendingStart = _pendingCaptureStarts.Begin(
                                command,
                                notifyOnFailure: false,
                                forceReconcileOnFailure: false,
                                previousTransportCommand: previousTransportCommand,
                                originConsumerId: consumerId,
                                originHadPreviousSubscription: hadPreviousSubscription,
                                originCaptureMouse: previousCaptureMouse,
                                originCaptureKeyboard: previousCaptureKeyboard);
                            waitTask = createdPendingStart.Value.Completion.Task;
                        }

                        _captureCoordinator.MarkCommandIssued(command);
                        commandToSend = command;
                    }
                    else
                    {
                        _pendingCaptureStarts.RegisterAsyncParticipant(
                            consumerId,
                            hadPreviousSubscription,
                            previousCaptureMouse,
                            previousCaptureKeyboard);
                        joinedExistingPendingStart = true;
                    }
                }

                if (commandToSend.Type != CaptureCommandType.None)
                {
                    try
                    {
                        SendCaptureCommand(
                            commandToSend,
                            requestId: createdPendingStart?.RequestId ?? 0,
                            throwOnFailure: commandToSend.Type == CaptureCommandType.Start);
                    }
                    catch
                    {
                        lock (_captureLock)
                        {
                            RestoreSubscription_NoLock(
                                consumerId,
                                hadPreviousSubscription,
                                previousCaptureMouse,
                                previousCaptureKeyboard);
                            _captureCoordinator.MarkTransportStopped();
                        }

                        _pendingCaptureStarts.ClearCurrent(createdPendingStart?.RequestId ?? 0);
                        throw;
                    }

                    if (commandToSend.Type == CaptureCommandType.Stop)
                    {
                        return;
                    }
                }
            }
            finally
            {
                _captureCommandGate.Release();
            }

            if (waitTask == null)
            {
                return;
            }

            try
            {
                await waitTask.WaitAsync(token);
            }
            catch (Exception ex) when (ShouldRetrySharedPendingStartFailure(
                ex,
                joinedExistingPendingStart,
                consumerId,
                mouse,
                keyboard))
            {
                continue;
            }
        }
    }

    public void StopCapture()
    {
        StopCapture(DefaultConsumerId);
    }

    public void StopCapture(string consumerId)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return;
        }

        CaptureCommand commandToSend = default;
        PendingCaptureStartRegistration? pendingStart = null;

        _captureCommandGate.Wait();
        try
        {
            lock (_captureLock)
            {
                _captureCoordinator.RemoveSubscription(consumerId);

                if (_pendingCaptureStarts.TryGetPendingTask() != null)
                {
                    _pendingCaptureStarts.MarkSubscriptionRemoved(consumerId);

                    if (!_captureCoordinator.HasSubscriptions)
                    {
                        AbortPendingCaptureStart_NoLock();
                    }

                    if (_captureCoordinator.HasSubscriptions)
                    {
                        return;
                    }
                }
                else
                {
                    var command = _captureCoordinator.GetRequiredCommand();
                    if (command.Type != CaptureCommandType.None)
                    {
                        if (command.Type == CaptureCommandType.Start)
                        {
                            var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                            pendingStart = _pendingCaptureStarts.Begin(
                                command,
                                notifyOnFailure: true,
                                forceReconcileOnFailure: true,
                                previousTransportCommand: previousTransportCommand);
                        }

                        _captureCoordinator.MarkCommandIssued(command);
                        commandToSend = command;
                    }
                }
            }

            if (commandToSend.Type != CaptureCommandType.None)
            {
                try
                {
                    if (!SendCaptureCommand(commandToSend, requestId: pendingStart?.RequestId ?? 0))
                    {
                        lock (_captureLock)
                        {
                            _captureCoordinator.MarkTransportStopped();
                        }

                        _pendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
                    }
                }
                catch
                {
                    lock (_captureLock)
                    {
                        _captureCoordinator.MarkTransportStopped();
                    }

                    _pendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
                    throw;
                }
            }
        }
        finally
        {
            _captureCommandGate.Release();
        }
    }

    private bool SendCaptureCommand(CaptureCommand command, int requestId = 0, bool throwOnFailure = false)
    {
        switch (command.Type)
        {
            case CaptureCommandType.Start:
                Log.Debug(
                    "[IpcClient] TX: StartCapture RequestId={RequestId} Mouse={Mouse} Keyboard={Keyboard}",
                    requestId,
                    command.CaptureMouse,
                    command.CaptureKeyboard);
                return Send(IpcOpCode.StartCapture, w =>
                {
                    w.Write(requestId);
                    w.Write(command.CaptureMouse);
                    w.Write(command.CaptureKeyboard);
                }, throwOnFailure);
            case CaptureCommandType.Stop:
                Log.Debug("[IpcClient] TX: StopCapture");
                return Send(IpcOpCode.StopCapture, throwOnFailure: throwOnFailure);
            default:
                return false;
        }
    }

    public void SimulateEvent(ushort type, ushort code, int value)
    {
        Log.Debug("[IpcClient] TX: SimulateEvent Type={Type} Code={Code} Value={Value}", type, code, value);
        Send(IpcOpCode.SimulateEvent, w =>
        {
            w.Write(type);
            w.Write(code);
            w.Write(value);
        }, throwOnFailure: true);
    }

    public void SimulateEvents(ReadOnlySpan<(ushort Type, ushort Code, int Value)> events)
    {
        if (!IsConnected)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Failed to send simulated events because the daemon connection is not available.");
        }

        lock (_writeLock)
        {
            try
            {
                foreach (var (type, code, value) in events)
                {
                    _writer!.Write((byte)IpcOpCode.SimulateEvent);
                    _writer.Write(type);
                    _writer.Write(code);
                    _writer.Write(value);
                }
                _stream!.Flush();
            }
            catch (Exception ex)
            {
                HandleSendFailure(ex, IpcOpCode.SimulateEvent, throwOnFailure: true);
            }
        }
    }

    public void ConfigureResolution(int width, int height)
    {
        Send(IpcOpCode.ConfigureResolution, w =>
        {
            w.Write(width);
            w.Write(height);
        }, throwOnFailure: true);
    }

    private bool Send(
        IpcOpCode op,
        Action<BinaryWriter>? writerAction = null,
        bool throwOnFailure = false)
    {
        if (!IsConnected)
        {
            if (throwOnFailure)
            {
                throw new IpcClientException(
                    IpcClientFailureReason.ConnectFailed,
                    $"Failed to send '{op}' because the daemon connection is not available.");
            }
            return false;
        }

        lock (_writeLock)
        {
            try
            {
                _writer!.Write((byte)op);
                writerAction?.Invoke(_writer);
                _stream!.Flush();
                return true;
            }
            catch (Exception ex)
            {
                HandleSendFailure(ex, op, throwOnFailure);
                return false;
            }
        }
    }

    public void Cleanup()
    {
        Cleanup(clearSubscriptions: true, disableReconnect: true);
    }

    private void Cleanup(bool clearSubscriptions, bool disableReconnect)
    {
        DropTransport();

        if (disableReconnect)
        {
            lock (_reconnectLock)
            {
                _reconnectEnabled = false;
            }
            _reconnectCts.Cancel();
        }

        lock (_captureLock)
        {
            if (clearSubscriptions)
            {
                _captureCoordinator.Clear();
            }
            else
            {
                _captureCoordinator.ResetTransportState();
            }
        }
    }

    private void HandleSendFailure(Exception ex, IpcOpCode op, bool throwOnFailure)
    {
        Log.Error(ex, "Failed to send IPC message: {OpCode}", op);
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
        }

        var failedPendingStart = _pendingCaptureStarts.TryFailCurrent(
            new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send IPC command '{op}'.",
                ex),
            out var notifyOnFailure);
        if (notifyOnFailure || !failedPendingStart)
        {
            RaiseErrorOccurredDeferred($"IPC send failed ({op}): {ex.Message}");
        }

        DropTransport(deferErrorNotifications: true);
        StartReconnectLoop();

        if (throwOnFailure)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send IPC command '{op}'.",
                ex);
        }
    }

    private void DropTransport(bool deferErrorNotifications = false)
    {
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
        }

        var failedPendingStart = _pendingCaptureStarts.TryFailCurrent(
            new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Daemon connection was lost during capture startup."),
            out var notifyOnFailure);
        if (notifyOnFailure && failedPendingStart)
        {
            if (deferErrorNotifications)
            {
                RaiseErrorOccurredDeferred("Daemon connection was lost during capture startup.");
            }
            else
            {
                RaiseErrorOccurredSafely("Daemon connection was lost during capture startup.");
            }
        }

        // DropTransport can run concurrently from read/send failure paths and Dispose().
        // Detaching references first avoids double-cancel/double-dispose races.
        var cts = Interlocked.Exchange(ref _cts, null);
        var reader = Interlocked.Exchange(ref _reader, null);
        var writer = Interlocked.Exchange(ref _writer, null);
        var stream = Interlocked.Exchange(ref _stream, null);
        var socket = Interlocked.Exchange(ref _socket, null);

        CancelSafely(cts);
        SafeDispose(reader);
        SafeDispose(writer);
        SafeDispose(stream);
        SafeDispose(socket);
        SafeDispose(cts);

        _readTask = null;
    }

    private void RaiseErrorOccurredDeferred(string message)
    {
        var handler = ErrorOccurred;
        if (handler is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            var gateAcquired = false;
            try
            {
                // Ensure callbacks are dispatched only after any in-flight capture command
                // exits its gate, avoiding re-entrant waits on the same gate.
                await _captureCommandGate.WaitAsync();
                gateAcquired = true;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                if (gateAcquired)
                {
                    _captureCommandGate.Release();
                }
            }

            InvokeErrorOccurredHandlersSafely(handler, message, "deferred notification");
        });
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
        EventHandler<string> handlers,
        string message,
        string notificationContext)
    {
        foreach (EventHandler<string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, message);
            }
            catch (Exception callbackError)
            {
                Log.Warning(
                    callbackError,
                    "[IpcClient] Error callback threw during {NotificationContext}",
                    notificationContext);
            }
        }
    }

    private static void CancelSafely(CancellationTokenSource? cts)
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

    private static void SafeDispose(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void StartReconnectLoop()
    {
        if (!_autoReconnect)
        {
            return;
        }

        lock (_reconnectLock)
        {
            if (!_reconnectEnabled || _disposed)
            {
                return;
            }

            if (_reconnectTask is { IsCompleted: false })
            {
                return;
            }

            _reconnectTask = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        var delay = TimeSpan.FromMilliseconds(250);
        var maxDelay = TimeSpan.FromSeconds(5);

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(token);
                    Log.Information("[IpcClient] Reconnected to daemon");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[IpcClient] Reconnect attempt failed");
                }

                await Task.Delay(delay, token);
                delay = TimeSpan.FromMilliseconds(Math.Min(maxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_reconnectLock)
            {
                _reconnectTask = null;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IpcClient));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cleanup();
        _reconnectCts.Dispose();
        _captureCommandGate.Dispose();
        _connectGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool ShouldRetrySharedPendingStartFailure(
        Exception exception,
        bool joinedExistingPendingStart,
        string consumerId,
        bool mouse,
        bool keyboard)
    {
        if (!joinedExistingPendingStart || exception is not InvalidOperationException)
        {
            return false;
        }

        lock (_captureLock)
        {
            return _captureCoordinator.TryGetSubscription(
                consumerId,
                out var currentCaptureMouse,
                out var currentCaptureKeyboard) &&
                currentCaptureMouse == mouse &&
                currentCaptureKeyboard == keyboard;
        }
    }

    private void AbortPendingCaptureStart_NoLock()
    {
        _captureCoordinator.MarkTransportStopped();

        _ = _pendingCaptureStarts.TryFailCurrent(
            new OperationCanceledException("Capture startup was cancelled before daemon acknowledgement."),
            out _);

        // Keep the shared socket alive. StopCapture is queued after the stale start and
        // tears daemon capture down once that delayed start completes.
        SendCaptureCommand(new CaptureCommand(CaptureCommandType.Stop));
    }

    private bool TryReconcileCaptureStateNow()
    {
        if (_disposed || !_captureCommandGate.Wait(0))
        {
            return false;
        }

        try
        {
            return TryDispatchReconcileCommandUnderGate();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[IpcClient] Immediate capture reconcile failed");
            return true;
        }
        finally
        {
            _captureCommandGate.Release();
        }
    }

    private void StartDeferredCaptureReconcile()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                await _captureCommandGate.WaitAsync(_reconnectCts.Token);
                try
                {
                    _ = TryDispatchReconcileCommandUnderGate();
                }
                finally
                {
                    _captureCommandGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
            Log.Warning(ex, "[IpcClient] Failed to reconcile capture state");
            }
        });
    }

    private void HandleCaptureStartedMessage(int startedRequestId)
    {
        Log.Debug("[IpcClient] RX: CaptureStarted RequestId={RequestId}", startedRequestId);
        if (_pendingCaptureStarts.TryComplete(startedRequestId, out var completedStart))
        {
            _ = completedStart.Completion.TrySetResult(true);
            StartDeferredCaptureReconcile();
            return;
        }

        Log.Debug("[IpcClient] Ignoring stale CaptureStarted for RequestId={RequestId}", startedRequestId);
    }

    private void HandleCaptureStartFailedMessage(int failedRequestId, string failureMessage)
    {
        var failureException = new InvalidOperationException(failureMessage);
        Log.Warning(
            "[IpcClient] RX: CaptureStartFailed RequestId={RequestId} Message={Message}",
            failedRequestId,
            failureMessage);

        var hasFailedPendingStart = _pendingCaptureStarts.TryFail(
            failedRequestId,
            out var failureContext);
        if (!hasFailedPendingStart)
        {
            Log.Debug("[IpcClient] Ignoring stale CaptureStartFailed for RequestId={RequestId}", failedRequestId);
            return;
        }

        var removedConsumersSinceStart = failureContext.RemovedConsumersSinceStart.Length == 0
            ? null
            : new HashSet<string>(failureContext.RemovedConsumersSinceStart, StringComparer.Ordinal);
        bool shouldReconcile;
        var rollbackChangedSubscriptions = false;
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
            foreach (var participant in failureContext.FailedAsyncParticipants)
            {
                if (!participant.ShouldRestoreOnFailure)
                {
                    continue;
                }

                if (removedConsumersSinceStart?.Contains(participant.ConsumerId) == true)
                {
                    continue;
                }

                rollbackChangedSubscriptions |= RestoreSubscription_NoLock(
                    participant.ConsumerId,
                    participant.HadPreviousSubscription,
                    participant.PreviousCaptureMouse,
                    participant.PreviousCaptureKeyboard);
            }

            var currentRequiredCommand = _captureCoordinator.GetRequiredCommand();
            shouldReconcile = failureContext.ForceReconcileOnFailure ||
                CaptureStartFailureReconciler.ShouldReconcile(
                    currentRequiredCommand,
                    failureContext.FailedCommand,
                    failureContext.FailedAsyncParticipants.Length == 0 &&
                        failureContext.FailedPreviousTransportCommand.Type == CaptureCommandType.Start,
                    failureContext.SubscriptionRemovedSinceStart,
                    rollbackChangedSubscriptions);
        }

        if (shouldReconcile && !TryReconcileCaptureStateNow())
        {
            StartDeferredCaptureReconcile();
        }

        if (failureContext.NotifyOnFailure)
        {
            try
            {
                RaiseErrorOccurredSafely(failureMessage);
            }
            finally
            {
                _ = failureContext.Completion.TrySetException(failureException);
            }
            return;
        }

        _ = failureContext.Completion.TrySetException(failureException);
    }

    private bool TryDispatchReconcileCommandUnderGate()
    {
        PendingCaptureStartRegistration? deferredPendingStart = null;
        CaptureCommand deferredCommand;

        lock (_captureLock)
        {
            if (_pendingCaptureStarts.TryGetPendingTask() != null)
            {
                return true;
            }

            deferredCommand = _captureCoordinator.GetRequiredCommand();
            if (deferredCommand.Type == CaptureCommandType.None)
            {
                return true;
            }

            if (deferredCommand.Type == CaptureCommandType.Start)
            {
                deferredPendingStart = _pendingCaptureStarts.Begin(deferredCommand, notifyOnFailure: true);
            }

            _captureCoordinator.MarkCommandIssued(deferredCommand);
        }

        try
        {
            SendCaptureCommand(
                deferredCommand,
                requestId: deferredPendingStart?.RequestId ?? 0,
                throwOnFailure: deferredCommand.Type == CaptureCommandType.Start);
        }
        catch
        {
            lock (_captureLock)
            {
                _captureCoordinator.MarkTransportStopped();
            }

            _pendingCaptureStarts.ClearCurrent(deferredPendingStart?.RequestId ?? 0);
            throw;
        }

        return true;
    }

    private bool RestoreSubscription_NoLock(
        string consumerId,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard)
    {
        var hasCurrentSubscription = _captureCoordinator.TryGetSubscription(
            consumerId,
            out var currentCaptureMouse,
            out var currentCaptureKeyboard);

        if (hadPreviousSubscription)
        {
            if (hasCurrentSubscription &&
                currentCaptureMouse == previousCaptureMouse &&
                currentCaptureKeyboard == previousCaptureKeyboard)
            {
                return false;
            }

            _captureCoordinator.SetSubscription(consumerId, previousCaptureMouse, previousCaptureKeyboard);
            return true;
        }

        if (!hasCurrentSubscription)
        {
            return false;
        }

        _captureCoordinator.RemoveSubscription(consumerId);
        return true;
    }
}
