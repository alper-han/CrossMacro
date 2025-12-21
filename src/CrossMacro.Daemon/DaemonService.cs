using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Daemon.Security;
using CrossMacro.Native.Evdev;
using CrossMacro.Native.Linux;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Daemon;

public class DaemonService
{
    [DllImport("libc", SetLastError = true)]
    private static extern int chown(string path, int owner, int group);
    private Socket? _socket;
    private readonly List<EvdevReader> _readers = new();
    private UInputDevice? _uInputDevice;
    private readonly object _lock = new();
    
    private readonly RateLimiter _rateLimiter = new(maxConnectionsPerWindow: 10, windowSeconds: 60, banSeconds: 60);
    private readonly AuditLogger _auditLogger = new();
    
    private uint _currentClientUid;
    private int _currentClientPid;
    private DateTime _sessionStart;

    public async Task RunAsync(CancellationToken token)
    {
        // Try primary systemd-managed path first
        var socketPath = IpcProtocol.DefaultSocketPath;
        var socketDir = Path.GetDirectoryName(socketPath);
        
        // Check if we can use the primary path
        bool usePrimaryPath = false;
        if (!string.IsNullOrEmpty(socketDir))
        {
            if (Directory.Exists(socketDir))
            {
                usePrimaryPath = true;
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(socketDir);
                    usePrimaryPath = true;
                    Log.Information("Created socket directory: {Dir}", socketDir);
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't create /run/crossmacro - likely not running via systemd
                    Log.Warning("Cannot create {Dir}, falling back to /tmp", socketDir);
                }
            }
        }

        // Fallback to /tmp for portable / AppImage deployments
        if (!usePrimaryPath)
        {
            socketPath = IpcProtocol.FallbackSocketPath;
            socketDir = Path.GetDirectoryName(socketPath);
            Log.Information("Using fallback socket path: {Path}", socketPath);
        }

        if (File.Exists(socketPath))
        {
            try
            {
                File.Delete(socketPath);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to cleanup existing socket: {Msg}", ex.Message);
            }
        }

        try
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _socket.Bind(new UnixDomainSocketEndPoint(socketPath));
            _socket.Listen(1);
            
            // Ensure user can access the socket
            try
            {
                // Try to find 'crossmacro' group GID
                int targetGid = -1;
                try 
                {
                    if (File.Exists("/etc/group"))
                    {
                        foreach (var line in File.ReadLines("/etc/group"))
                        {
                            if (line.StartsWith("crossmacro:"))
                            {
                                var parts = line.Split(':');
                                if (parts.Length >= 3 && int.TryParse(parts[2], out int gid))
                                {
                                    targetGid = gid;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to lookup crossmacro group GID: {Msg}", ex.Message);
                }

                if (targetGid != -1)
                {
                    // -1 means don't change owner
                    if (chown(socketPath, -1, targetGid) == 0)
                    {
                        Log.Information("Set socket group to 'crossmacro' (GID: {Gid})", targetGid);
                    }
                    else
                    {
                        Log.Warning("Failed to chown socket to crossmacro group. Errno: {Err}", Marshal.GetLastWin32Error());
                    }
                }

                // Restricted: RW for User and Group (660)
                // We assume the service runs as root (or helper user) and the UI user is in the 'input' group.
                try 
                {
                    if (OperatingSystem.IsLinux())
                    {
                       SetUnixSocketPermissions(socketPath);
                    }
                }
                catch (Exception ex)
                {
                     Log.Warning("Failed to set file mode: {Msg}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to set socket permissions: {Msg}", ex.Message);
            }

            Log.Information("Listening on {SocketPath}", socketPath);
            
            // Signal systemd that we're ready to accept connections
            SystemdNotify.Ready();
            SystemdNotify.Status("Listening for client connections");

            // Allow concurrent connections? No, strictly one controller.
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _socket.AcceptAsync(token);
                    
                    // Get peer credentials
                    var creds = PeerCredentials.GetCredentials(client);
                    if (creds == null)
                    {
                        Log.Warning("[Security] Failed to get peer credentials, rejecting connection");
                        _auditLogger.LogSecurityViolation(0, 0, "PEER_CRED_FAILED");
                        client.Dispose();
                        continue;
                    }
                    
                    var (uid, gid, pid) = creds.Value;
                    var executable = PeerCredentials.GetProcessExecutable(pid);
                    Log.Information("Client connected: UID={Uid}, GID={Gid}, PID={Pid}, Exe={Exe}", 
                        uid, gid, pid, executable ?? "unknown");
                    
                    // Reject root connections
                    if (uid == 0)
                    {
                        Log.Warning("[Security] Root connection rejected (UID=0)");
                        _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "ROOT_REJECTED");
                        client.Dispose();
                        continue;
                    }
                    
                    // Rate limiting
                    if (_rateLimiter.IsRateLimited(uid))
                    {
                        Log.Warning("[Security] UID {Uid} is rate limited", uid);
                        _auditLogger.LogRateLimited(uid, pid);
                        client.Dispose();
                        continue;
                    }
                    
                    // Check group membership
                    if (!PeerCredentials.IsUserInGroup(uid, "crossmacro"))
                    {
                        Log.Warning("[Security] UID {Uid} is not in 'crossmacro' group", uid);
                        _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "NOT_IN_GROUP");
                        client.Dispose();
                        continue;
                    }
                    
                    // Polkit authorization
                    // Note: 'org.freedesktop.policykit.owner' annotation in policy file allows
                    // 'crossmacro' user to check authorization for other users.
                    var polkitAuthorized = await PolkitChecker.CheckAuthorizationAsync(
                        uid, pid, PolkitChecker.Actions.InputCapture);
                    
                    if (!polkitAuthorized)
                    {
                        Log.Warning("[Security] Polkit authorization denied for UID {Uid}", uid);
                        _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "POLKIT_DENIED");
                        client.Dispose();
                        continue;
                    }
                    
                    // All security checks passed
                    _currentClientUid = uid;
                    _currentClientPid = pid;
                    _sessionStart = DateTime.UtcNow;
                    _auditLogger.LogConnectionAttempt(uid, pid, executable, true);
                    _rateLimiter.RecordSuccess(uid);
                    
                    try 
                    {
                        await HandleClientAsync(client, token);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Client session error");
                    }
                    finally
                    {
                        StopCapture(); // Ensure capture stops if client disconnects
                        var sessionDuration = DateTime.UtcNow - _sessionStart;
                        _auditLogger.LogDisconnect(uid, pid, sessionDuration);
                        client.Dispose();
                        Log.Information("Client disconnected (session: {Duration}s)", sessionDuration.TotalSeconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Accept failed");
                }
            }
        } // End of top-level try
        finally
        {
            if (File.Exists(socketPath))
            {
                try
                {
                    File.Delete(socketPath);
                    Log.Information("Socket cleaned up");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to clean up socket on exit");
                }
            }
            _socket?.Dispose();
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken token)
    {
        using var stream = new NetworkStream(client);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        // Handshake
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

        // Initialize UInput device if needed
        if (_uInputDevice == null)
        {
            try 
            {
                _uInputDevice = new UInputDevice();
                _uInputDevice.CreateVirtualInputDevice();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create UInput device");
                writer.Write((byte)IpcOpCode.Error);
                writer.Write($"Failed to init UInput: {ex.Message}");
                return;
            }
        }

        // Read loop task
        
        var clientCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        
        var readTask = Task.Run(() => ReadLoop(reader, clientCts.Token), clientCts.Token);
        
        try
        {
            await readTask;
        }
        finally
        {
            clientCts.Cancel();
        }
    }

    private void ReadLoop(BinaryReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var opcodeByte = reader.ReadByte(); // Implementation detail: Block read
                var opcode = (IpcOpCode)opcodeByte;

                switch (opcode)
                {
                    case IpcOpCode.StartCapture:
                    {
                        var captureMouse = reader.ReadBoolean();
                        var captureKb = reader.ReadBoolean();
                        _auditLogger.LogCaptureStart(_currentClientUid, _currentClientPid, captureMouse, captureKb);
                        StartCapture(reader.BaseStream, captureMouse, captureKb);
                        break;
                    }
                    case IpcOpCode.StopCapture:
                    {
                        _auditLogger.LogCaptureStop(_currentClientUid, _currentClientPid);
                        StopCapture();
                        break;
                    }
                    case IpcOpCode.ConfigureResolution:
                    {
                        var width = reader.ReadInt32();
                        var height = reader.ReadInt32();
                        ConfigureDevice(width, height);
                        break;
                    }
                    case IpcOpCode.SimulateEvent:
                    {
                        var type = reader.ReadUInt16();
                        var code = reader.ReadUInt16();
                        var value = reader.ReadInt32();
                        _uInputDevice?.SendEvent(type, code, value);
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
            Log.Debug("[DaemonService] Client disconnected (EndOfStream)");
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "[DaemonService] Client disconnected (IOException)");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ReadLoop");
        }
    }

    private void StartCapture(Stream stream, bool mouse, bool kb)
    {
        lock (_lock)
        {
            StopCapture(); // Clear existing

            var writer = new BinaryWriter(stream); // Warning: don't close this writer as it closes stream
            
            var devices = InputDeviceHelper.GetAvailableDevices();
            var targetDevices = devices.Where(d => (mouse && d.IsMouse) || (kb && d.IsKeyboard)).ToList();
            
            Log.Information("Starting capture on {Count} devices", targetDevices.Count);

            foreach (var dev in targetDevices)
            {
                try 
                {
                    var evReader = new EvdevReader(dev.Path, dev.Name);
                    evReader.EventReceived += (sender, e) => 
                    {
                        // Send event to client
                        try 
                        {
                            lock (stream) // Sync write
                            {
                                writer.Write((byte)IpcOpCode.InputEvent);
                                writer.Write((byte)GetEventType(e.type, e.code));
                                writer.Write((int)e.code);
                                writer.Write(e.value);
                                writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                stream.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "[DaemonService] Failed to write input event (stream likely closed)");
                        }
                    };
                    evReader.Start();
                    _readers.Add(evReader);
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to open {Path}: {Msg}", dev.Path, ex.Message);
                }
            }
        }
    }

    private void ConfigureDevice(int width, int height)
    {
         lock (_lock)
         {
             try 
             {
                 if (_uInputDevice != null)
                 {
                     _uInputDevice.Dispose();
                     _uInputDevice = null;
                 }
                 
                 _uInputDevice = new UInputDevice(width, height);
                 _uInputDevice.CreateVirtualInputDevice();
                 Log.Information("Reconfigured UInput device with resolution {W}x{H}", width, height);
             }
             catch (Exception ex)
             {
                 Log.Error(ex, "Failed to configure UInput device");
             }
         }
    }

    private void StopCapture()
    {
        lock (_lock)
        {
            foreach (var r in _readers)
            {
                r.Dispose();
            }
            _readers.Clear();
            Log.Information("Stopped capture");
        }
    }
    
    
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void SetUnixSocketPermissions(string socketPath)
    {
        File.SetUnixFileMode(socketPath, 
            UnixFileMode.UserRead | UnixFileMode.UserWrite | 
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        Log.Information("Socket permissions set to 660 (User+Group RW)");
    }

    // Helper to map native type to Core InputEventType
    private byte GetEventType(ushort type, ushort code)
    {
        // Native constants
        const ushort EV_KEY = 0x01;
        const ushort EV_REL = 0x02;
        const ushort REL_WHEEL = 0x08;
        const ushort EV_SYN = 0x00;

        if (type == EV_KEY) return 1; // InputEventType.Key
        
        if (type == EV_REL)
        {
            if (code == REL_WHEEL) return 4; // InputEventType.MouseScroll
            return 3; // InputEventType.MouseMove
        }
        
        if (type == EV_SYN) return 0; // InputEventType.Sync
        
        return 0; 
    }
}
