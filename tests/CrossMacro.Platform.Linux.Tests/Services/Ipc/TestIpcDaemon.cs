using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

internal sealed class TestIpcDaemon : IAsyncDisposable
{
    private readonly string _socketPath;
    private readonly HandshakeBehavior _handshakeBehavior;
    private readonly Socket _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<CapturedCommand> _commands = new();
    private readonly SemaphoreSlim _commandSignal = new(0);
    private Task? _serverTask;
    private Socket? _clientSocket;
    private int _startCaptureCount;

    private TestIpcDaemon(string socketPath, HandshakeBehavior handshakeBehavior)
    {
        _socketPath = socketPath;
        _handshakeBehavior = handshakeBehavior;
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    }

    public static async Task<TestIpcDaemon> StartAsync(
        string socketPath,
        HandshakeBehavior handshakeBehavior = HandshakeBehavior.Success)
    {
        var daemon = new TestIpcDaemon(socketPath, handshakeBehavior);
        await daemon.StartInternalAsync();
        return daemon;
    }

    public CapturedCommand[] GetCommandsSnapshot() => _commands.ToArray();

    /// <summary>Writes raw bytes to the connected client after the handshake.</summary>
    public async Task WriteToClientAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var socket = _clientSocket ?? throw new InvalidOperationException("No client is connected.");
        _ = await socket.SendAsync(bytes, SocketFlags.None, cancellationToken);
    }

    public async Task WaitForCommandCountAsync(int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (_commands.Count < expected)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out waiting for {expected} IPC command(s). Received {_commands.Count}."));
            }

            _ = await _commandSignal.WaitAsync(remaining, _cts.Token);
        }
    }

    private async Task StartInternalAsync()
    {
        var dir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(1);
        _serverTask = RunServerAsync(_cts.Token);

        await Task.Yield();
    }

    private async Task RunServerAsync(CancellationToken token)
    {
        try
        {
            _clientSocket = await _listener.AcceptAsync(token);
            using var stream = new NetworkStream(_clientSocket, ownsSocket: false);
            using var reader = new BinaryReader(stream);
            using var writer = new BinaryWriter(stream);

            var handshakeOp = (IpcOpCode)reader.ReadByte();
            var protocolVersion = reader.ReadInt32();
            if (handshakeOp is not IpcOpCode.Handshake || protocolVersion != IpcProtocol.ProtocolVersion)
            {
                writer.Write((byte)IpcOpCode.Error);
                writer.Write("Invalid handshake");
                await stream.FlushAsync(token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.ErrorResponse)
            {
                writer.Write((byte)IpcOpCode.Error);
                writer.Write("Authorization denied");
                await stream.FlushAsync(token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.ProtocolMismatch)
            {
                writer.Write((byte)IpcOpCode.Handshake);
                writer.Write(IpcProtocol.ProtocolVersion + 1);
                await stream.FlushAsync(token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.NoResponse)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return;
            }

            writer.Write((byte)IpcOpCode.Handshake);
            writer.Write(IpcProtocol.ProtocolVersion);
            await stream.FlushAsync(token);

            if (_handshakeBehavior is HandshakeBehavior.StopReadingAfterHandshake)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return;
            }

            while (!token.IsCancellationRequested)
            {
                IpcOpCode opCode;
                try
                {
                    opCode = (IpcOpCode)reader.ReadByte();
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                switch (opCode)
                {
                    case IpcOpCode.StartCapture:
                        var requestId = reader.ReadInt32();
                        var captureMouse = reader.ReadBoolean();
                        var captureKeyboard = reader.ReadBoolean();
                        _commands.Enqueue(new CapturedCommand(
                            OpCode: opCode,
                            RequestId: requestId,
                            CaptureMouse: captureMouse,
                            CaptureKeyboard: captureKeyboard));
                        _ = _commandSignal.Release();
                        _startCaptureCount++;
                        if (_handshakeBehavior is HandshakeBehavior.FailFirstStartAfterDelay && _startCaptureCount is 1)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(300), TimeProvider.System, token);
                            if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                            {
                                return;
                            }
                            break;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.FailSecondStartAfterDelay && _startCaptureCount is 2)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(300), TimeProvider.System, token);
                            if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                            {
                                return;
                            }
                            break;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.DelayAllCaptureStartAcks)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(200), TimeProvider.System, token);
                        }

                        if (!TryWriteCaptureStarted(writer, stream, requestId))
                        {
                            return;
                        }
                        break;
                    case IpcOpCode.StopCapture:
                        _commands.Enqueue(new CapturedCommand(OpCode: opCode));
                        _ = _commandSignal.Release();
                        break;
                    case IpcOpCode.SimulateEvent:
                        _commands.Enqueue(new CapturedCommand(
                            OpCode: opCode,
                            Type: reader.ReadUInt16(),
                            Code: reader.ReadUInt16(),
                            Value: reader.ReadInt32()));
                        _ = _commandSignal.Release();
                        break;
                    case IpcOpCode.SimulateEventBatch:
                        var simulationRequestId = reader.ReadInt32();
                        var eventCount = reader.ReadInt32();
                        for (var i = 0; i < eventCount; i++)
                        {
                            var type = reader.ReadUInt16();
                            var code = reader.ReadUInt16();
                            var value = reader.ReadInt32();
                            var delayAfterMicroseconds = reader.ReadInt64();
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                RequestId: simulationRequestId,
                                Type: type,
                                Code: code,
                                Value: value,
                                DelayAfterMicroseconds: delayAfterMicroseconds));
                            _ = _commandSignal.Release();
                        }

                        if (_handshakeBehavior is HandshakeBehavior.DropSimulationBatchBeforeAck)
                        {
                            _clientSocket?.Dispose();
                            return;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.HoldSimulationBatchWithoutAck)
                        {
                            await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                            return;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.FailSimulationBatch)
                        {
                            if (!TryWriteSimulationBatchFailed(writer, stream, simulationRequestId, "Simulated batch failure"))
                            {
                                return;
                            }

                            break;
                        }

                        var acknowledgedEventCount = _handshakeBehavior is HandshakeBehavior.CorruptSimulationBatchCount
                            ? eventCount + 1
                            : eventCount;

                        if (!TryWriteSimulationBatchCompleted(
                                writer,
                                stream,
                                simulationRequestId,
                                acknowledgedEventCount))
                        {
                            return;
                        }

                        break;
                    case IpcOpCode.ConfigureResolution:
                        _commands.Enqueue(new CapturedCommand(
                            OpCode: opCode,
                            Width: reader.ReadInt32(),
                            Height: reader.ReadInt32()));
                        _ = _commandSignal.Release();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
    }

    private static bool TryWriteCaptureStarted(BinaryWriter writer, Stream stream, int requestId)
    {
        try
        {
            writer.Write((byte)IpcOpCode.CaptureStarted);
            writer.Write(requestId);
            stream.Flush();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool TryWriteCaptureStartFailed(BinaryWriter writer, Stream stream, int requestId, string message)
    {
        try
        {
            writer.Write((byte)IpcOpCode.CaptureStartFailed);
            writer.Write(requestId);
            writer.Write(message);
            stream.Flush();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool TryWriteSimulationBatchCompleted(
        BinaryWriter writer,
        Stream stream,
        int requestId,
        int eventCount)
    {
        try
        {
            writer.Write((byte)IpcOpCode.SimulationBatchCompleted);
            writer.Write(requestId);
            writer.Write(eventCount);
            stream.Flush();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool TryWriteSimulationBatchFailed(BinaryWriter writer, Stream stream, int requestId, string message)
    {
        try
        {
            writer.Write((byte)IpcOpCode.SimulationBatchFailed);
            writer.Write(requestId);
            writer.Write(message);
            stream.Flush();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_clientSocket is not null)
        {
            try
            {
                _clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _clientSocket.Close(0);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        try
        {
            _listener.Close(0);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (_serverTask is not null)
        {
            try
            {
                await _serverTask.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TimeProvider.System,
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        _cts.Dispose();
        _commandSignal.Dispose();

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
    }
}
