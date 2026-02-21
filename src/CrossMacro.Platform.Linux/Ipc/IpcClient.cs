using System;
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
    private bool _disposed;
    private bool _reconnectEnabled = true;
    private const string DefaultConsumerId = "default";
    private const int HandshakeTimeoutMs = 5000;

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _socket?.Connected ?? false;

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

                lock (_captureLock)
                {
                    SendCaptureCommand(_captureCoordinator.ResetTransportStateAndGetCommand());
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

                    case IpcOpCode.Error:
                        var msg = _reader.ReadString();
                        Log.Warning("[IpcClient] RX: Error from daemon: {Message}", msg);
                        ErrorOccurred?.Invoke(this, msg);
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
                ErrorOccurred?.Invoke(this, "Connection lost: " + ex.Message);
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

        lock (_captureLock)
        {
            var command = _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);
            SendCaptureCommand(command);
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

        lock (_captureLock)
        {
            var command = _captureCoordinator.RemoveSubscription(consumerId);
            SendCaptureCommand(command);
        }
    }

    private void SendCaptureCommand(CaptureCommand command)
    {
        switch (command.Type)
        {
            case CaptureCommandType.Start:
                Log.Debug("[IpcClient] TX: StartCapture Mouse={Mouse} Keyboard={Keyboard}",
                    command.CaptureMouse, command.CaptureKeyboard);
                Send(IpcOpCode.StartCapture, w =>
                {
                    w.Write(command.CaptureMouse);
                    w.Write(command.CaptureKeyboard);
                }, throwOnFailure: false);
                break;
            case CaptureCommandType.Stop:
                Log.Debug("[IpcClient] TX: StopCapture");
                Send(IpcOpCode.StopCapture, throwOnFailure: false);
                break;
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

    private void Send(
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
            return;
        }

        lock (_writeLock)
        {
            try
            {
                _writer!.Write((byte)op);
                writerAction?.Invoke(_writer);
                _stream!.Flush();
            }
            catch (Exception ex)
            {
                HandleSendFailure(ex, op, throwOnFailure);
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
                _captureCoordinator.ResetTransportStateAndGetCommand();
            }
        }
    }

    private void HandleSendFailure(Exception ex, IpcOpCode op, bool throwOnFailure)
    {
        Log.Error(ex, "Failed to send IPC message: {OpCode}", op);
        ErrorOccurred?.Invoke(this, $"IPC send failed ({op}): {ex.Message}");

        DropTransport();
        StartReconnectLoop();

        if (throwOnFailure)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send IPC command '{op}'.",
                ex);
        }
    }

    private void DropTransport()
    {
        _cts?.Cancel();
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _socket?.Dispose();
        _cts?.Dispose();

        _reader = null;
        _writer = null;
        _stream = null;
        _socket = null;
        _cts = null;
        _readTask = null;
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
        _connectGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
