using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Daemon.Contracts.Ipc;

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

            if (!lifecycle.TryInitialize())
            {
                return;
            }

            await Task.Run(
                () => lifecycle.Run(uid, pid, client, clientCts.Token),
                clientCts.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("[SessionHandler] Session canceled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Session error");
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

        public bool TryInitialize()
        {
            return TryCompleteHandshake() && TryInitializeVirtualDevice();
        }

        public void Run(uint uid, int pid, Socket client, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var opcode = ReadNextOpcode();
                    ProcessRequest(opcode, uid, pid);
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
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ReadLoop");
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

        private void ProcessRequest(IpcOpCode opcode, uint uid, int pid)
        {
            try
            {
                DispatchCommand(opcode, uid, pid);
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
                Log.Error(ex, "[SessionHandler] Command processing failed for opcode {Op}", opcode);
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
            if (opcode != IpcOpCode.Handshake)
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

        private bool TryInitializeVirtualDevice()
        {
            try
            {
                _virtualDevice.Configure(0, 0);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create UInput device");
                _session.Writer.Write((byte)IpcOpCode.Error);
                _session.Writer.Write($"Failed to init UInput: {ex.Message}");
                return false;
            }
        }

        private void DispatchCommand(IpcOpCode opcode, uint uid, int pid)
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
                    HandleConfigureResolutionCommand();
                    break;
                case IpcOpCode.SimulateEvent:
                    HandleSimulateEventCommand(uid, pid);
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
                Log.Error(ex, "[SessionHandler] Capture manager threw during StartCapture");
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

        private void HandleConfigureResolutionCommand()
        {
            var width = _session.Reader.ReadInt32();
            var height = _session.Reader.ReadInt32();
            _virtualDevice.Configure(width, height);
        }

        private void HandleSimulateEventCommand(uint uid, int pid)
        {
            var type = _session.Reader.ReadUInt16();
            var code = _session.Reader.ReadUInt16();
            var value = _session.Reader.ReadInt32();
            _virtualDevice.SendEvent(type, code, value);
            _security.LogSimulation(uid, pid, type, code, value);
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
