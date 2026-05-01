using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Daemon.Services;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Linux.Native;
using CrossMacro.Infrastructure.Linux.Native.Systemd;

namespace CrossMacro.Daemon;

public class DaemonService
{
    private const int SingleClientListenBacklog = 1;

    private Socket? _socket;
    private int _shutdownRequested;
    
    private readonly ISecurityService _security;
    private readonly ILinuxPermissionService _permissionService;
    private readonly ISessionHandlerFactory _sessionHandlerFactory;
    private readonly string _socketPath;

    public DaemonService(
        ISecurityService security,
        ILinuxPermissionService permissionService,
        ISessionHandlerFactory sessionHandlerFactory,
        string socketPath)
    {
        _security = security;
        _permissionService = permissionService;
        _sessionHandlerFactory = sessionHandlerFactory;
        _socketPath = socketPath;
    }

    public async Task RunAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        var socketPath = _socketPath;
        CleanupSocketFile(socketPath);
        ResetRuntimeState();

        using var shutdownRegistration = token.Register(static state => ((DaemonService)state!).RequestShutdown(), this);

        try
        {
            var listeningSocket = CreateListeningSocket(socketPath);
            _socket = listeningSocket;

            _permissionService.ConfigureSocketPermissions(socketPath);

            Log.Information("Listening on {SocketPath}", socketPath);
            SystemdNotify.Ready();
            SystemdNotify.Status("Listening for client connections");

            await RunAcceptLoopAsync(listeningSocket, token);
        }
        finally
        {
            CloseListeningSocket();
            CleanupSocketFile(socketPath, logOnSuccess: true);
        }
    }

    private static Socket CreateListeningSocket(string socketPath)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(socketPath));
        socket.Listen(SingleClientListenBacklog);
        return socket;
    }

    private async Task RunAcceptLoopAsync(Socket listeningSocket, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await AcceptAndRunSingleClientAsync(listeningSocket, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex) when (token.IsCancellationRequested)
            {
                Log.Debug(ex, "Accept loop stopped during shutdown");
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Accept failed");
            }
        }
    }

    private async Task AcceptAndRunSingleClientAsync(Socket listeningSocket, CancellationToken token)
    {
        var client = await AcceptClientAsync(listeningSocket, token);
        try
        {
            await RunClientSessionAsync(client, token);
        }
        finally
        {
            DisposeSocket(client);
        }
    }

    private static async Task<Socket> AcceptClientAsync(Socket listeningSocket, CancellationToken token)
    {
        return await listeningSocket.AcceptAsync(token);
    }

    private async Task RunClientSessionAsync(Socket client, CancellationToken token)
    {
        var session = ClientSessionAudit.CreatePending();

        try
        {
            var validationResult = await _security.ValidateConnectionAsync(client);
            if (validationResult is null)
            {
                return;
            }

            session = session.MarkValidated(validationResult.Value.Uid, validationResult.Value.Pid);

            var sessionHandler = _sessionHandlerFactory.Create();
            await sessionHandler.RunAsync(client, session.Uid, session.Pid, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("Client session canceled during shutdown");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Client session error");
        }
        finally
        {
            if (session.IsValidated)
            {
                _security.LogDisconnect(session.Uid, session.Pid, session.GetDuration());
            }
        }
    }

    private readonly record struct ClientSessionAudit(bool IsValidated, uint Uid, int Pid, DateTime SessionStart)
    {
        public static ClientSessionAudit CreatePending() =>
            new(false, 0, 0, DateTime.UtcNow);

        public ClientSessionAudit MarkValidated(uint uid, int pid) =>
            this with
            {
                IsValidated = true,
                Uid = uid,
                Pid = pid,
                SessionStart = DateTime.UtcNow
            };

        public TimeSpan GetDuration() => DateTime.UtcNow - SessionStart;
    }

    private static void CleanupSocketFile(string socketPath, bool logOnSuccess = false)
    {
        if (!File.Exists(socketPath))
        {
            return;
        }

        try
        {
            File.Delete(socketPath);
            if (logOnSuccess)
            {
                Log.Information("Socket cleaned up");
            }
        }
        catch (Exception ex)
        {
            if (logOnSuccess)
            {
                Log.Error(ex, "Failed to clean up socket on exit");
                return;
            }

            Log.Warning("Failed to cleanup existing socket: {Msg}", ex.Message);
        }
    }

    private void ResetRuntimeState()
    {
        _shutdownRequested = 0;
        _socket = null;
    }

    private void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        Log.Information("Stopping daemon listener...");
        SystemdNotify.Status("Stopping daemon");
        CloseListeningSocket();
    }

    private void CloseListeningSocket()
    {
        var socket = Interlocked.Exchange(ref _socket, null);
        DisposeSocket(socket);
    }

    private static void DisposeSocket(Socket? socket)
    {
        if (socket is null)
        {
            return;
        }

        try
        {
            socket.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
