
namespace CrossMacro.Daemon.Services;

public class SessionHandler : ISessionHandler
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
            catch
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
                _maxBufferedCaptureEvents,
                new DaemonInputEventEncoder());
            var lifecycle = new SessionLifecycle(session, _security, _virtualDevice, _inputCapture);

            if (!await lifecycle.TryInitializeAsync(clientCts.Token).ConfigureAwait(false))
            {
                return;
            }

            await Task.Run(
                () => lifecycle.RunAsync(uid, pid, client, clientCts.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("[SessionHandler] Session canceled");
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Session error");
        }
    }

    private sealed class SessionLifecycle
    {
        private readonly DaemonProtocolSession _session;
        private readonly ISecurityService _security;
        private readonly IVirtualDeviceManager _virtualDevice;
        private readonly IInputCaptureManager _inputCapture;

        public SessionLifecycle(
            DaemonProtocolSession session,
            ISecurityService security,
            IVirtualDeviceManager virtualDevice,
            IInputCaptureManager inputCapture)
        {
            _session = session;
            _security = security;
            _virtualDevice = virtualDevice;
            _inputCapture = inputCapture;
        }

        public async Task<bool> TryInitializeAsync(CancellationToken token)
        {
            return TryCompleteHandshake() && await TryInitializeVirtualDeviceAsync(token).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                Log.LogError(ex, "Error in ReadLoop");
            }
            finally
            {
                FinalizeSession(client);
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
                await DispatchCommandAsync(opcode, uid, pid, token).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "[SessionHandler] Command processing failed for opcode {Op}", opcode);
                throw;
            }
        }

        private void FinalizeSession(Socket client)
        {
            _session.MarkDisconnected();

            try
            {
                _inputCapture.StopCapture();
            }
            finally
            {
                DisposeClientSocket(client);
            }
        }

        private bool TryCompleteHandshake()
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
                return false;
            }

            _session.Writer.Write((byte)IpcOpCode.Handshake);
            _session.Writer.Write(IpcProtocol.ProtocolVersion);
            _session.Stream.Flush();
            return true;
        }

        private async Task<bool> TryInitializeVirtualDeviceAsync(CancellationToken token)
        {
            try
            {
                await _virtualDevice.ConfigureAsync(0, 0, token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Failed to create UInput device");
                _session.Writer.Write((byte)IpcOpCode.Error);
                _session.Writer.Write($"Failed to init UInput: {ex.Message}");
                return false;
            }
        }

        private async Task DispatchCommandAsync(IpcOpCode opcode, uint uid, int pid, CancellationToken token)
        {
            switch (opcode)
            {
                case IpcOpCode.StartCapture:
                    HandleStartCaptureCommand(uid, pid);
                    break;
                case IpcOpCode.StopCapture:
                    HandleStopCaptureCommand(uid, pid);
                    break;
                case IpcOpCode.ConfigureResolution:
                    await HandleConfigureResolutionCommandAsync(token).ConfigureAwait(false);
                    break;
                case IpcOpCode.SimulateEvent:
                    await HandleSimulateEventCommandAsync(uid, pid, token).ConfigureAwait(false);
                    break;
                case IpcOpCode.SimulateEventBatch:
                    await HandleSimulateEventBatchCommandAsync(uid, pid, token).ConfigureAwait(false);
                    break;
                default:
                    Log.Warning("Unknown OpCode: {Op}", opcode);
                    break;
            }
        }

        private void HandleStartCaptureCommand(uint uid, int pid)
        {
            var requestId = _session.Reader.ReadInt32();
            var captureMouse = _session.Reader.ReadBoolean();
            var captureKb = _session.Reader.ReadBoolean();
            _security.LogCaptureStart(uid, pid, captureMouse, captureKb);

            var requestGeneration = _session.CaptureForwarding.BeginPendingGeneration();

            CaptureStartResult result;
            try
            {
                result = _inputCapture.StartCapture(
                    captureMouse,
                    captureKb,
                    _session.CaptureForwarding.CreateEventForwarder(requestGeneration, _session));
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "[SessionHandler] Capture manager threw during StartCapture");
                result = CaptureStartResult.Failed(
                    "Failed to start capture due to internal error: " + ex.Message);
            }

            using (_session.WriterGate.Enter())
            {
                if (result.Success)
                {
                    var activation = _session.CaptureForwarding.ActivateGeneration(requestGeneration);

                    _session.Writer.Write((byte)IpcOpCode.CaptureStarted);
                    _session.Writer.Write(requestId);

                    if (activation.DroppedPendingCaptureEvents > 0)
                    {
                        Log.Warning(
                            "[SessionHandler] Dropped {DroppedCount} pending capture event(s) while waiting for startup acknowledgement (Generation={Generation})",
                            activation.DroppedPendingCaptureEvents,
                            requestGeneration);
                    }

                    if (activation.HasBufferedEvents)
                    {
                        while (activation.BufferedEvents!.Count > 0)
                        {
                            var bufferedEvent = activation.BufferedEvents.Dequeue();
                            _session.WriteInputEvent(bufferedEvent);
                        }
                    }
                }
                else
                {
                    _session.Writer.Write((byte)IpcOpCode.CaptureStartFailed);
                    _session.Writer.Write(requestId);
                    _session.Writer.Write(result.ErrorMessage ?? "Failed to start capture.");
                    _session.CaptureForwarding.ResetAfterFailedStart(requestGeneration);
                }

                _session.Stream.Flush();
            }
        }

        private void HandleStopCaptureCommand(uint uid, int pid)
        {
            _security.LogCaptureStop(uid, pid);
            using (_session.WriterGate.Enter())
            {
                _session.CaptureForwarding.Stop();
            }

            _inputCapture.StopCapture();
        }

        private async Task HandleConfigureResolutionCommandAsync(CancellationToken token)
        {
            var width = _session.Reader.ReadInt32();
            var height = _session.Reader.ReadInt32();
            await _virtualDevice.ConfigureAsync(width, height, token).ConfigureAwait(false);
        }

        private async Task HandleSimulateEventCommandAsync(uint uid, int pid, CancellationToken token)
        {
            var type = _session.Reader.ReadUInt16();
            var code = _session.Reader.ReadUInt16();
            var value = _session.Reader.ReadInt32();
            await _virtualDevice.SendEventAsync(type, code, value, token).ConfigureAwait(false);
            _security.LogSimulation(uid, pid, type, code, value);
        }

        private async Task HandleSimulateEventBatchCommandAsync(uint uid, int pid, CancellationToken token)
        {
            var requestId = _session.Reader.ReadInt32();
            try
            {
                var events = ReadSimulationBatchEvents();
                await _virtualDevice.SendEventsAsync(events, token).ConfigureAwait(false);

                using (_session.WriterGate.Enter())
                {
                    _session.Writer.Write((byte)IpcOpCode.SimulationBatchCompleted);
                    _session.Writer.Write(requestId);
                    _session.Stream.Flush();
                }

                foreach (var inputEvent in events)
                {
                    _security.LogSimulation(uid, pid, inputEvent.Type, inputEvent.Code, inputEvent.Value);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "[SessionHandler] Simulation batch failed");
                using (_session.WriterGate.Enter())
                {
                    _session.Writer.Write((byte)IpcOpCode.SimulationBatchFailed);
                    _session.Writer.Write(requestId);
                    _session.Writer.Write(ex.Message);
                    _session.Stream.Flush();
                }
            }
        }

        private IpcSimulationRequest[] ReadSimulationBatchEvents()
        {
            var eventCount = _session.Reader.ReadInt32();
            if (eventCount is <= 0 or > IpcProtocol.MaxSimulationBatchEvents)
            {
                throw new InvalidDataException(
                    $"Simulation batch event count {eventCount} is outside the allowed range 1-{IpcProtocol.MaxSimulationBatchEvents}.");
            }

            var events = new IpcSimulationRequest[eventCount];
            var totalDelayMs = 0;
            for (var i = 0; i < events.Length; i++)
            {
                var inputEvent = new IpcSimulationRequest
                {
                    Type = _session.Reader.ReadUInt16(),
                    Code = _session.Reader.ReadUInt16(),
                    Value = _session.Reader.ReadInt32(),
                    DelayAfterMs = _session.Reader.ReadInt32(),
                };

                if (inputEvent.DelayAfterMs is < 0 or > IpcProtocol.MaxSimulationBatchDelayMs)
                {
                    throw new InvalidDataException(
                        $"Simulation batch delay {inputEvent.DelayAfterMs}ms is outside the allowed range 0-{IpcProtocol.MaxSimulationBatchDelayMs}ms.");
                }

                totalDelayMs += inputEvent.DelayAfterMs;
                if (totalDelayMs > IpcProtocol.MaxSimulationBatchTotalDelayMs)
                {
                    throw new InvalidDataException(
                        $"Simulation batch total delay {totalDelayMs}ms exceeds the allowed maximum of {IpcProtocol.MaxSimulationBatchTotalDelayMs}ms.");
                }

                events[i] = inputEvent;
            }

            return events;
        }

        private static void DisposeClientSocket(Socket client)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // Best effort teardown; session is already fail-closed.
            }
        }
    }
}
