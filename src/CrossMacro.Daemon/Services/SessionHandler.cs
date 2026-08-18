
namespace CrossMacro.Daemon.Services;

internal sealed class SessionHandler : ISessionHandler
{
    private const int DefaultMaxBufferedCaptureEvents = 1024;
    private readonly ISecurityService _security;
    private readonly IVirtualDeviceManager _virtualDevice;
    private readonly IInputCaptureManager _inputCapture;
    private readonly int _maxBufferedCaptureEvents;

    public SessionHandler(
        ISecurityService security,
        IVirtualDeviceManager virtualDevice,
        IInputCaptureManager inputCapture,
        int maxBufferedCaptureEvents = DefaultMaxBufferedCaptureEvents)
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
            var lifecycle = new SessionLifecycle(session, _security, _virtualDevice, _inputCapture);

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
        IInputCaptureManager inputCapture)
    {
        private readonly DaemonProtocolSession _session = session;
        private readonly IVirtualDeviceManager _virtualDevice = virtualDevice;
        private readonly IInputCaptureManager _inputCapture = inputCapture;
        private readonly SessionCommandDispatcher _commands = new(session, security, virtualDevice, inputCapture);

        public async Task<bool> TryInitializeAsync(CancellationToken token)
        {
            return await TryCompleteHandshakeAsync(token).ConfigureAwait(false) &&
                   await TryInitializeVirtualDeviceAsync(token).ConfigureAwait(false);
        }

        public async Task RunAsync(uint uid, int pid, Socket client, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var opcode = ReadNextOpcode();
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
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "Error in ReadLoop");
            }
            finally
            {
                await FinalizeSessionAsync(client).ConfigureAwait(false);
            }
        }

        private IpcOpCode ReadNextOpcode()
        {
            return (IpcOpCode)_session.Reader.ReadByte();
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
                DisposeClientSocket(client);
                await _session.CaptureForwarding.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> TryCompleteHandshakeAsync(CancellationToken token)
        {
            var opcode = (IpcOpCode)_session.Reader.ReadByte();
            if (opcode is not IpcOpCode.Handshake)
            {
                Log.Warning("Invalid handshake opcode: {Op}", opcode);
                return false;
            }

            var version = _session.Reader.ReadInt32();
            if (version != IpcProtocol.ProtocolVersion)
            {
                Log.Warning("Protocol mismatch. Client: {C}, Server: {S}", version, IpcProtocol.ProtocolVersion);
                _session.Writer.Write((byte)IpcOpCode.Error);
                _session.Writer.Write("Protocol version mismatch");
                await _session.Stream.FlushAsync(token).ConfigureAwait(false);
                return false;
            }

            _session.Writer.Write((byte)IpcOpCode.Handshake);
            _session.Writer.Write(IpcProtocol.ProtocolVersion);
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
                _session.Writer.Write((byte)IpcOpCode.Error);
                _session.Writer.Write($"Failed to init UInput: {ex.Message}");
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
