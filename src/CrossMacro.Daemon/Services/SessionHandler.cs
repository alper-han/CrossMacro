using System;
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
    private readonly ISecurityService _security;
    private readonly IVirtualDeviceManager _virtualDevice;
    private readonly IInputCaptureManager _inputCapture;

    public SessionHandler(
        ISecurityService security, 
        IVirtualDeviceManager virtualDevice, 
        IInputCaptureManager inputCapture)
    {
        _security = security;
        _virtualDevice = virtualDevice;
        _inputCapture = inputCapture;
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

            object writerLock = new object();
            bool disconnected = false;

            // Define the capture callback here to close over 'writer' and 'writerLock'
            Action<UInputNative.input_event> onEventValue = (e) =>
            {
                if (disconnected) return;

                try
                {
                    lock (writerLock)
                    {
                        if (disconnected) return;

                        writer.Write((byte)IpcOpCode.InputEvent);
                        writer.Write((byte)GetEventType(e.type, e.code));
                        writer.Write((int)e.code);
                        writer.Write(e.value);
                        writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
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
                () => ReadLoop(reader, uid, pid, onEventValue, () => disconnected = true, clientCts.Token),
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

    private void ReadLoop(BinaryReader reader, uint uid, int pid, Action<UInputNative.input_event> onEvent, Action onDisconnect, CancellationToken token)
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
                    {
                        var captureMouse = reader.ReadBoolean();
                        var captureKb = reader.ReadBoolean();
                        _security.LogCaptureStart(uid, pid, captureMouse, captureKb);
                        _inputCapture.StartCapture(captureMouse, captureKb, onEvent);
                        break;
                    }
                    case IpcOpCode.StopCapture:
                    {
                        _security.LogCaptureStop(uid, pid);
                        _inputCapture.StopCapture();
                        break;
                    }
                    case IpcOpCode.ConfigureResolution:
                    {
                        var width = reader.ReadInt32();
                        var height = reader.ReadInt32();
                        _virtualDevice.Configure(width, height);
                        break;
                    }
                    case IpcOpCode.SimulateEvent:
                    {
                        var type = reader.ReadUInt16();
                        var code = reader.ReadUInt16();
                        var value = reader.ReadInt32();
                        _virtualDevice.SendEvent(type, code, value);
                        break;
                    }
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
        
        if (type == UInputNative.EV_SYN) 
            return (byte)InputEventType.Sync;
        
        return 0; 
    }
}
