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
    private int _disposed;

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
        var started = TimeProvider.System.GetTimestamp();
        while (_commands.Count < expected)
        {
            var remaining = timeout - TimeProvider.System.GetElapsedTime(started);
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
                IpcMessageCodec.WriteError(writer, "Invalid handshake");
                await stream.FlushAsync(token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.ErrorResponse)
            {
                IpcMessageCodec.WriteError(writer, "Authorization denied");
                await stream.FlushAsync(token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.ProtocolMismatch)
            {
                await stream.WriteAsync(IpcHandshakeWireCodec.CreateHandshake(IpcProtocol.ProtocolVersion + 1), token);
                return;
            }

            if (_handshakeBehavior is HandshakeBehavior.NoResponse)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return;
            }

            await stream.WriteAsync(IpcHandshakeWireCodec.CreateHandshake(), token);

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
                        var (requestId, captureMouse, captureKeyboard) = IpcMessageCodec.ReadCaptureStartPayload(reader);
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
                            if (!TryReply(writer, stream, w => IpcMessageCodec.WriteRequestFailure(w, IpcOpCode.CaptureStartFailed, requestId, "Simulated delayed start failure")))
                            {
                                return;
                            }
                            break;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.FailSecondStartAfterDelay && _startCaptureCount is 2)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(300), TimeProvider.System, token);
                            if (!TryReply(writer, stream, w => IpcMessageCodec.WriteRequestFailure(w, IpcOpCode.CaptureStartFailed, requestId, "Simulated delayed start failure")))
                            {
                                return;
                            }
                            break;
                        }

                        if (_handshakeBehavior is HandshakeBehavior.DelayAllCaptureStartAcks)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(200), TimeProvider.System, token);
                        }

                        if (!TryReply(writer, stream, w => IpcMessageCodec.WriteCaptureStarted(w, requestId)))
                        {
                            return;
                        }
                        break;
                    case IpcOpCode.StopCapture:
                        _commands.Enqueue(new CapturedCommand(OpCode: opCode));
                        _ = _commandSignal.Release();
                        break;
                    case IpcOpCode.SimulateEvent:
                        var (singleType, singleCode, singleValue) = IpcMessageCodec.ReadSingleSimulationPayload(reader);
                        _commands.Enqueue(new CapturedCommand(
                            OpCode: opCode,
                            Type: singleType,
                            Code: singleCode,
                            Value: singleValue));
                        _ = _commandSignal.Release();
                        break;
                    case IpcOpCode.SimulateEventBatch:
                        var simulationRequestId = IpcMessageCodec.ReadRequestId(reader);
                        var batch = IpcMessageCodec.ReadSimulationBatch(reader);
                        var eventCount = batch.Events.Count;
                        foreach (var batchEvent in batch.Events)
                        {
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                RequestId: simulationRequestId,
                                Type: batchEvent.Type,
                                Code: batchEvent.Code,
                                Value: batchEvent.Value,
                                DelayAfterMicroseconds: batchEvent.DelayAfterMicroseconds));
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
                            if (!TryReply(writer, stream, w => IpcMessageCodec.WriteRequestFailure(w, IpcOpCode.SimulationBatchFailed, simulationRequestId, "Simulated batch failure")))
                            {
                                return;
                            }

                            break;
                        }

                        var acknowledgedEventCount = _handshakeBehavior is HandshakeBehavior.CorruptSimulationBatchCount
                            ? eventCount + 1
                            : eventCount;

                        if (!TryReply(
                                writer,
                                stream,
                                w => IpcMessageCodec.WriteSimulationBatchCompleted(w, simulationRequestId, acknowledgedEventCount)))
                        {
                            return;
                        }

                        break;
                    case IpcOpCode.ConfigureResolution:
                        var (width, height) = IpcMessageCodec.ReadResolutionPayload(reader);
                        _commands.Enqueue(new CapturedCommand(OpCode: opCode, Width: width, Height: height));
                        _ = _commandSignal.Release();
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or IOException or SocketException)
        {
            // The peer closed the connection or the daemon was disposed: the fake stops serving.
            System.Diagnostics.Debug.WriteLine($"Test IPC daemon stopped serving: {ex.GetType().Name}");
        }
    }

    private static bool TryReply(BinaryWriter writer, Stream stream, Action<BinaryWriter> write)
    {
        try
        {
            write(writer);
            stream.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is 1)
        {
            return;
        }

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
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        }

        _cts.Dispose();
        _commandSignal.Dispose();

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
    }
}
