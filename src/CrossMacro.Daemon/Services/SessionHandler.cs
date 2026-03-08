using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services; // For InputEventType
using CrossMacro.Platform.Linux.Native.UInput; // For UInputNative
using Serilog;

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

        // Handshake
        try 
        {
            var opcode = (IpcOpCode)reader.ReadByte();
            if (opcode != IpcOpCode.Handshake)
            {
                Log.Warning("Invalid handshake opcode: {Op}", opcode);
                return;
            }
            
            var version = reader.ReadInt32();
            if (version != IpcProtocol.ProtocolVersion)
            {
                Log.Warning("Protocol mismatch. Client: {C}, Server: {S}", version, IpcProtocol.ProtocolVersion);
                writer.Write((byte)IpcOpCode.Error);
                writer.Write("Protocol version mismatch");
                return;
            }

            writer.Write((byte)IpcOpCode.Handshake);
            writer.Write(IpcProtocol.ProtocolVersion);
            writer.Flush();

            // Default Device Init
            try 
            {

               _virtualDevice.Configure(0, 0); // Default relative
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create UInput device");
                writer.Write((byte)IpcOpCode.Error);
                writer.Write($"Failed to init UInput: {ex.Message}");
                return;
            }

            var writerGate = new OrderedWriteGate();
            bool disconnected = false;
            var captureState = new CaptureForwardingState();

            Action<UInputNative.input_event> CreateCaptureEventForwarder(int generation) => (e) =>
            {
                if (disconnected) return;

                try
                {
                    using (writerGate.Enter())
                    {
                        if (disconnected) return;

                        if (generation == captureState.PendingGeneration)
                        {
                            if (captureState.BufferedCaptureEvents.Count >= _maxBufferedCaptureEvents)
                            {
                                captureState.BufferedCaptureEvents.Dequeue();
                                captureState.DroppedPendingCaptureEvents++;
                            }

                            captureState.BufferedCaptureEvents.Enqueue(e);
                            return;
                        }

                        if (!captureState.CaptureForwardingEnabled || generation != captureState.ActiveGeneration)
                        {
                            return;
                        }

                        WriteInputEvent(writer, e);
                        stream.Flush();
                    }
                }
                catch (IOException)
                {
                    disconnected = true;
                    Log.Debug("[SessionHandler] Stream closed, stopping event forwarding");
                }
                catch (Exception ex)
                {
                    disconnected = true;
                    Log.Debug(ex, "[SessionHandler] Failed to write input event");
                }
            };

            await Task.Run(
                () => ReadLoop(
                    reader,
                    writer,
                    stream,
                    writerGate,
                    captureState,
                    uid,
                    pid,
                    CreateCaptureEventForwarder,
                    () => disconnected = true,
                    clientCts.Token),
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
        finally
        {
            // Cleanup is handled in ReadLoop's finally block
        }

    }

    private void ReadLoop(
        BinaryReader reader,
        BinaryWriter writer,
        Stream stream,
        OrderedWriteGate writerGate,
        CaptureForwardingState captureState,
        uint uid,
        int pid,
        Func<int, Action<UInputNative.input_event>> createCaptureEventForwarder,
        Action onDisconnect,
        CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var opcodeByte = reader.ReadByte(); // Block read
                var opcode = (IpcOpCode)opcodeByte;

                switch (opcode)
                {
                    case IpcOpCode.StartCapture:
                        HandleStartCaptureCommand(
                            reader,
                            writer,
                            stream,
                            writerGate,
                            captureState,
                            uid,
                            pid,
                            createCaptureEventForwarder);
                        break;
                    case IpcOpCode.StopCapture:
                        HandleStopCaptureCommand(writerGate, captureState, uid, pid);
                        break;
                    case IpcOpCode.ConfigureResolution:
                        HandleConfigureResolutionCommand(reader);
                        break;
                    case IpcOpCode.SimulateEvent:
                        HandleSimulateEventCommand(reader);
                        break;
                    default:
                         Log.Warning("Unknown OpCode: {Op}", opcode);
                         break;
                }
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
            onDisconnect();
            _inputCapture.StopCapture();
        }
    }

    private void HandleStartCaptureCommand(
        BinaryReader reader,
        BinaryWriter writer,
        Stream stream,
        OrderedWriteGate writerGate,
        CaptureForwardingState captureState,
        uint uid,
        int pid,
        Func<int, Action<UInputNative.input_event>> createCaptureEventForwarder)
    {
        var requestId = reader.ReadInt32();
        var captureMouse = reader.ReadBoolean();
        var captureKb = reader.ReadBoolean();
        int requestGeneration;
        _security.LogCaptureStart(uid, pid, captureMouse, captureKb);
        using (writerGate.Enter())
        {
            requestGeneration = ++captureState.NextGeneration;
            captureState.PendingGeneration = requestGeneration;
            ResetPendingBuffer(captureState);
        }

        CaptureStartResult result;
        try
        {
            result = _inputCapture.StartCapture(
                captureMouse,
                captureKb,
                createCaptureEventForwarder(requestGeneration));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SessionHandler] Capture manager threw during StartCapture");
            result = CaptureStartResult.Failed(
                "Failed to start capture due to internal error: " + ex.Message);
        }

        using (writerGate.Enter())
        {
            if (result.Success)
            {
                writer.Write((byte)IpcOpCode.CaptureStarted);
                writer.Write(requestId);
                if (captureState.DroppedPendingCaptureEvents > 0)
                {
                    Log.Warning(
                        "[SessionHandler] Dropped {DroppedCount} pending capture event(s) while waiting for startup acknowledgement (Generation={Generation})",
                        captureState.DroppedPendingCaptureEvents,
                        requestGeneration);
                }
                captureState.ActiveGeneration = requestGeneration;
                captureState.PendingGeneration = 0;
                captureState.CaptureForwardingEnabled = true;
                while (captureState.BufferedCaptureEvents.Count > 0)
                {
                    var bufferedEvent = captureState.BufferedCaptureEvents.Dequeue();
                    WriteInputEvent(writer, bufferedEvent);
                }
                captureState.DroppedPendingCaptureEvents = 0;
            }
            else
            {
                writer.Write((byte)IpcOpCode.CaptureStartFailed);
                writer.Write(requestId);
                writer.Write(result.ErrorMessage ?? "Failed to start capture.");
                if (captureState.PendingGeneration == requestGeneration)
                {
                    captureState.PendingGeneration = 0;
                }
                captureState.ActiveGeneration = 0;
                captureState.CaptureForwardingEnabled = false;
                ResetPendingBuffer(captureState);
            }

            stream.Flush();
        }
    }

    private void HandleStopCaptureCommand(
        OrderedWriteGate writerGate,
        CaptureForwardingState captureState,
        uint uid,
        int pid)
    {
        _security.LogCaptureStop(uid, pid);
        using (writerGate.Enter())
        {
            captureState.PendingGeneration = 0;
            captureState.ActiveGeneration = 0;
            captureState.CaptureForwardingEnabled = false;
            ResetPendingBuffer(captureState);
        }
        _inputCapture.StopCapture();
    }

    private void HandleConfigureResolutionCommand(BinaryReader reader)
    {
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        _virtualDevice.Configure(width, height);
    }

    private void HandleSimulateEventCommand(BinaryReader reader)
    {
        var type = reader.ReadUInt16();
        var code = reader.ReadUInt16();
        var value = reader.ReadInt32();
        _virtualDevice.SendEvent(type, code, value);
    }

    private byte GetEventType(ushort type, ushort code)
    {
        if (type == UInputNative.EV_KEY)
        {
            if (UInputNative.IsMouseButton(code))
                return (byte)InputEventType.MouseButton;
            return (byte)InputEventType.Key;
        }
        
        if (type == UInputNative.EV_REL)
        {
            if (code == UInputNative.REL_WHEEL) 
                return (byte)InputEventType.MouseScroll;
            return (byte)InputEventType.MouseMove;
        }

        if (type == UInputNative.EV_ABS)
        {
            // Some devices (e.g. QEMU USB Tablet) report pointer motion as ABS_X/ABS_Y.
            if (code == UInputNative.ABS_X || code == UInputNative.ABS_Y)
                return (byte)InputEventType.MouseMove;
        }
        
        if (type == UInputNative.EV_SYN) 
            return (byte)InputEventType.Sync;
        
        return (byte)InputEventType.Unknown;
    }

    private byte WriteInputEvent(BinaryWriter writer, UInputNative.input_event inputEvent)
    {
        writer.Write((byte)IpcOpCode.InputEvent);
        var eventType = GetEventType(inputEvent.type, inputEvent.code);
        writer.Write(eventType);
        writer.Write((int)inputEvent.code);
        writer.Write(inputEvent.value);
        writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return eventType;
    }

    private static void ResetPendingBuffer(CaptureForwardingState captureState)
    {
        captureState.BufferedCaptureEvents.Clear();
        captureState.DroppedPendingCaptureEvents = 0;
    }

    private sealed class CaptureForwardingState
    {
        public int NextGeneration { get; set; }
        public int PendingGeneration { get; set; }
        public int ActiveGeneration { get; set; }
        public bool CaptureForwardingEnabled { get; set; }
        public int DroppedPendingCaptureEvents { get; set; }
        public Queue<UInputNative.input_event> BufferedCaptureEvents { get; } = new();
    }
}
