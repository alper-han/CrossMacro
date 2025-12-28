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
    private Socket? _socket;
    private NetworkStream? _stream;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private readonly Lock _writeLock = new();

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _socket?.Connected ?? false;

    public async Task ConnectAsync(CancellationToken token)
    {
        if (IsConnected) return;

        // Try primary systemd-managed path first, then fallback
        string socketPath;
        if (File.Exists(IpcProtocol.DefaultSocketPath))
        {
            socketPath = IpcProtocol.DefaultSocketPath;
        }
        else if (File.Exists(IpcProtocol.FallbackSocketPath))
        {
            socketPath = IpcProtocol.FallbackSocketPath;
            Log.Information("Using fallback socket path: {Path}", socketPath);
        }
        else
        {
            throw new FileNotFoundException(
                $"Daemon socket not found. Checked:\n" +
                $"  - {IpcProtocol.DefaultSocketPath}\n" +
                $"  - {IpcProtocol.FallbackSocketPath}\n" +
                $"Is the CrossMacro daemon service running?");
        }

        try
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await _socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token);
            
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
                throw new Exception($"Daemon handshake error: {msg}");
            }
            if (opcode != IpcOpCode.Handshake)
            {
                throw new Exception($"Unexpected handshake opcode: {opcode}");
            }
            var version = _reader.ReadInt32();
            if (version != IpcProtocol.ProtocolVersion)
            {
                throw new Exception($"Protocol version mismatch. Daemon: {version}, Client: {IpcProtocol.ProtocolVersion}");
            }

            Log.Information("Connected to CrossMacro Daemon");

            // Start read loop
            _cts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            Cleanup();
            throw new Exception("Failed to connect to daemon", ex);
        }
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
                        ErrorOccurred?.Invoke(this, msg);
                        break;
                        
                    default:
                        Log.Warning("Unknown opcode received from Daemon: {Op}", opcode);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Log.Error(ex, "IPC Read Loop Error");
                ErrorOccurred?.Invoke(this, "Connection lost: " + ex.Message);
            }
        }
    }

    private int _captureRequestCount = 0;
    private readonly Lock _captureLock = new();

    public void StartCapture(bool mouse, bool keyboard)
    {
        lock (_captureLock)
        {
            _captureRequestCount++;
            if (_captureRequestCount == 1)
            {
                Send(IpcOpCode.StartCapture, w =>
                {
                    w.Write(mouse);
                    w.Write(keyboard);
                });
            }
            else if (mouse || keyboard)
            {
                // Re-send to update capture flags
                Send(IpcOpCode.StartCapture, w =>
                {
                    w.Write(mouse);
                    w.Write(keyboard);
                });
            }
        }
    }

    public void StopCapture()
    {
        lock (_captureLock)
        {
            if (_captureRequestCount > 0)
            {
                _captureRequestCount--;
            }
            
            if (_captureRequestCount == 0)
            {
                Send(IpcOpCode.StopCapture);
            }
        }
    }

    public void SimulateEvent(ushort type, ushort code, int value)
    {
        Send(IpcOpCode.SimulateEvent, w =>
        {
            w.Write(type);
            w.Write(code);
            w.Write(value);
        });
    }

    public void ConfigureResolution(int width, int height)
    {
        Send(IpcOpCode.ConfigureResolution, w =>
        {
            w.Write(width);
            w.Write(height);
        });
    }

    private void Send(IpcOpCode op, Action<BinaryWriter>? writerAction = null)
    {
        if (!IsConnected) return;

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
                Log.Error(ex, "Failed to send IPC message");
            }
        }
    }

    public void Cleanup()
    {
        _cts?.Cancel();
        _socket?.Dispose();
        _socket = null;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
