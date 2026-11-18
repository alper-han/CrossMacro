
namespace CrossMacro.Daemon.Services.Sessions;

internal sealed class SessionHandler : ISessionHandler
{
    private const int DefaultMaxBufferedCaptureEvents = 1024;
    private static readonly TimeSpan DefaultClientReadTimeout = TimeSpan.FromSeconds(5);
    private readonly ISecurityService _security;
    private readonly IVirtualDeviceManager _virtualDevice;
    private readonly IInputCaptureManager _inputCapture;
    private readonly int _maxBufferedCaptureEvents;
    private readonly TimeSpan _clientReadTimeout;

    public SessionHandler(
        ISecurityService security,
        IVirtualDeviceManager virtualDevice,
        IInputCaptureManager inputCapture,
        int maxBufferedCaptureEvents = DefaultMaxBufferedCaptureEvents,
        TimeSpan? clientReadTimeout = null)
    {
        _security = security;
        _virtualDevice = virtualDevice;
        _inputCapture = inputCapture;
        if (maxBufferedCaptureEvents <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBufferedCaptureEvents),
                maxBufferedCaptureEvents,
                "Buffered capture event limit must be greater than zero.");
        }

        _maxBufferedCaptureEvents = maxBufferedCaptureEvents;
        _clientReadTimeout = clientReadTimeout ?? DefaultClientReadTimeout;
        if (_clientReadTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clientReadTimeout),
                clientReadTimeout,
                "Client read timeout must be greater than zero.");
        }
    }

    public async Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
    {
        using var stream = new NetworkStream(client);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);
        using var clientCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var cancelRegistration = clientCts.Token.Register(static state =>
        {
            if (state is not Socket socket)
            {
                return;
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Best effort to unblock any pending stream reads on shutdown.
            }
        }, client);

        try
        {
            var session = new DaemonProtocolSession(
                reader,
                writer,
                stream,
                _maxBufferedCaptureEvents);
            var lifecycle = new SessionLifecycle(session, _security, _virtualDevice, _inputCapture, _clientReadTimeout);

            if (!await lifecycle.TryInitializeAsync(clientCts.Token).ConfigureAwait(false))
            {
                return;
            }

            await lifecycle.RunAsync(uid, pid, client, clientCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("[SessionHandler] Session canceled");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Session error");
        }
    }

    private sealed class SessionLifecycle(
        DaemonProtocolSession session,
        ISecurityService security,
        IVirtualDeviceManager virtualDevice,
        IInputCaptureManager inputCapture,
        TimeSpan clientReadTimeout)
    {
        private readonly DaemonProtocolSession _session = session;
        private readonly IVirtualDeviceManager _virtualDevice = virtualDevice;
        private readonly IInputCaptureManager _inputCapture = inputCapture;
        private readonly TimeSpan _clientReadTimeout = clientReadTimeout;
        private readonly SessionCommandDispatcher _commands = new(session, security, virtualDevice, inputCapture);
        private readonly byte[] _opcodeBuffer = new byte[1];

        public async Task<bool> TryInitializeAsync(CancellationToken token)
        {
            return await TryCompleteHandshakeAsync(token).ConfigureAwait(false) &&
                   await TryInitializeVirtualDeviceAsync(token).ConfigureAwait(false) &&
                   await SendHandshakeAcknowledgementAsync(token).ConfigureAwait(false);
        }

        public async Task RunAsync(uint uid, int pid, Socket client, CancellationToken token)
        {
            try
            {
                // Opcodes are awaited without a deadline, but a client that stalls in the
                // middle of a frame must not pin a worker thread inside a synchronous read.
                client.ReceiveTimeout = (int)_clientReadTimeout.TotalMilliseconds;
                while (!token.IsCancellationRequested)
                {
                    var opcode = await ReadNextOpcodeAsync(token).ConfigureAwait(false);
                    await ProcessRequestAsync(opcode, uid, pid, token).ConfigureAwait(false);
                }
            }
            catch (EndOfStreamException)
            {
                Log.Debug("[SessionHandler] Client disconnected (EndOfStream)");
            }
            catch (IOException ex)
            {
                Log.Debug(ex, "[SessionHandler] Client disconnected (IOException)");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Log.Debug("[SessionHandler] Session canceled");
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                Log.Debug("[SessionHandler] Session socket closed during shutdown");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "Error in ReadLoop");
            }
            finally
            {
                await FinalizeSessionAsync(client).ConfigureAwait(false);
            }
        }

        private async Task<IpcOpCode> ReadNextOpcodeAsync(CancellationToken token)
        {
            if (await _session.Stream.ReadAsync(_opcodeBuffer, token).ConfigureAwait(false) is 0)
            {
                throw new EndOfStreamException();
            }

            return (IpcOpCode)_opcodeBuffer[0];
        }

        private async Task ProcessRequestAsync(IpcOpCode opcode, uint uid, int pid, CancellationToken token)
        {
            try
            {
                await _commands.DispatchAsync(opcode, uid, pid, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (EndOfStreamException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[SessionHandler] Command processing failed for opcode {Op}", opcode);
                throw;
            }
        }

        private async Task FinalizeSessionAsync(Socket client)
        {
            _session.MarkDisconnected();

            try
            {
                _inputCapture.StopCapture();
            }
            finally
            {
                await ReleasePressedKeysAsync().ConfigureAwait(false);
                DisposeClientSocket(client);
                await _session.CaptureForwarding.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task ReleasePressedKeysAsync()
        {
            try
            {
                await _virtualDevice.ReleasePressedKeysAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[SessionHandler] Failed to release keys left pressed by the client");
            }
        }

        private async Task<bool> TryCompleteHandshakeAsync(CancellationToken token)
        {
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            handshakeCts.CancelAfter(_clientReadTimeout);

            try
            {
                var opcode = (IpcOpCode)await IpcHandshakeWireCodec.ReadByteAsync(_session.Stream, handshakeCts.Token).ConfigureAwait(false);
                if (opcode is not IpcOpCode.Handshake)
                {
                    Log.Warning("Invalid handshake opcode: {Op}", opcode);
                    return false;
                }

                var version = await IpcHandshakeWireCodec.ReadInt32Async(_session.Stream, handshakeCts.Token).ConfigureAwait(false);
                if (version != IpcProtocol.ProtocolVersion)
                {
                    Log.Warning("Protocol mismatch. Client: {C}, Server: {S}", version, IpcProtocol.ProtocolVersion);
                    IpcMessageCodec.WriteError(_session.Writer, "Protocol version mismatch");
                    await _session.Stream.FlushAsync(handshakeCts.Token).ConfigureAwait(false);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Log.Warning("Client did not complete the handshake within {Timeout}", _clientReadTimeout);
                return false;
            }
        }

        private async Task<bool> SendHandshakeAcknowledgementAsync(CancellationToken token)
        {
            _session.Writer.Write(IpcHandshakeWireCodec.CreateHandshake());
            await _session.Stream.FlushAsync(token).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> TryInitializeVirtualDeviceAsync(CancellationToken token)
        {
            try
            {
                await _virtualDevice.EnsureInitializedAsync(token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "Failed to create UInput device");
                IpcMessageCodec.WriteError(_session.Writer, $"Failed to init UInput: {ex.Message}");
                await _session.Stream.FlushAsync(token).ConfigureAwait(false);
                return false;
            }
        }

        private static void DisposeClientSocket(Socket client)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Best effort teardown; session is already fail-closed.
            }
        }
    }
}
