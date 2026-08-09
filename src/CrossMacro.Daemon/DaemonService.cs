
using System.Threading.Channels;

namespace CrossMacro.Daemon;

internal sealed class DaemonService(
    ISecurityService security,
    ILinuxPermissionService permissionService,
    ISessionHandlerFactory sessionHandlerFactory,
    string socketPath)
{
    private const int ClientListenBacklog = 16;
    private const int ClientQueueCapacity = 16;
    private const int ClientWorkerCount = 4;

    private Socket? _socket;
    private int _shutdownRequested;

    private readonly ISecurityService _security = security;
    private readonly ILinuxPermissionService _permissionService = permissionService;
    private readonly ISessionHandlerFactory _sessionHandlerFactory = sessionHandlerFactory;
    private readonly string _socketPath = socketPath;

    public async Task RunAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        var configuredSocketPath = _socketPath;
        CleanupSocketFile(configuredSocketPath);
        ResetRuntimeState();

        using var shutdownRegistration = token.Register(static state => ((DaemonService)state!).RequestShutdown(), this);

        try
        {
            var listeningSocket = CreateListeningSocket(configuredSocketPath);
            _socket = listeningSocket;

            _permissionService.ConfigureSocketPermissions(configuredSocketPath);

            Log.Information("Listening on {SocketPath}", configuredSocketPath);
            SystemdNotify.Ready();
            SystemdNotify.Status("Listening for client connections");

            await RunAcceptLoopAsync(listeningSocket, token).ConfigureAwait(false);
        }
        finally
        {
            CloseListeningSocket();
            CleanupSocketFile(configuredSocketPath, logOnSuccess: true);
        }
    }

    private static Socket CreateListeningSocket(string socketPath)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(socketPath));
            socket.Listen(ClientListenBacklog);
            return socket;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            socket.Dispose();
            throw;
        }
    }

    private async Task RunAcceptLoopAsync(Socket listeningSocket, CancellationToken token)
    {
        using var activeSessionGate = new SemaphoreSlim(1, 1);
        // NSS may block on LDAP/SSSD; bounded workers keep the accept loop responsive.
        var clientQueue = Channel.CreateBounded<Socket>(new BoundedChannelOptions(ClientQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });
        var workers = Enumerable.Range(0, ClientWorkerCount)
            .Select(_ => ProcessClientQueueAsync(clientQueue.Reader, activeSessionGate, token))
            .ToArray();

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await AcceptClientAsync(listeningSocket, token).ConfigureAwait(false);
                    if (!clientQueue.Writer.TryWrite(client))
                    {
                        Log.Warning("Client queue is full; rejecting a new client connection");
                        DisposeSocket(client);
                    }
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
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.LogError(ex, "Accept failed");
                }
            }
        }
        finally
        {
            CompleteClientQueue(clientQueue.Writer);
            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally
            {
                DrainClientQueue(clientQueue.Reader);
                activeSessionGate.Dispose();
            }
        }
    }

    private async Task ProcessClientQueueAsync(
        ChannelReader<Socket> clientQueue,
        SemaphoreSlim activeSessionGate,
        CancellationToken token)
    {
        try
        {
            await foreach (var client in clientQueue.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await RunClientSessionAsync(client, activeSessionGate, token).ConfigureAwait(false);
                }
                finally
                {
                    DisposeSocket(client);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("Client worker stopped during shutdown");
        }
    }

    private static void CompleteClientQueue(ChannelWriter<Socket> clientQueue)
    {
        if (!clientQueue.TryComplete())
        {
            Log.Debug("Client queue was already completed");
        }
    }

    private static void DrainClientQueue(ChannelReader<Socket> clientQueue)
    {
        while (clientQueue.TryRead(out var client))
        {
            DisposeSocket(client);
        }
    }

    private static async Task<Socket> AcceptClientAsync(Socket listeningSocket, CancellationToken token)
    {
        return await listeningSocket.AcceptAsync(token).ConfigureAwait(false);
    }

    private async Task RunClientSessionAsync(
        Socket client,
        SemaphoreSlim activeSessionGate,
        CancellationToken token)
    {
        var session = ClientSessionAudit.CreatePending();

        try
        {
            var validationResult = await _security.ValidateConnectionAsync(client, token).ConfigureAwait(false);
            if (validationResult is null)
            {
                return;
            }

            session = session.MarkValidated(validationResult.Value.Uid, validationResult.Value.Pid);

            await activeSessionGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var sessionHandler = _sessionHandlerFactory.Create();
                await sessionHandler.RunAsync(client, session.Uid, session.Pid, token).ConfigureAwait(false);
            }
            finally
            {
                ReleaseSessionGate(activeSessionGate);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Log.Debug("Client session canceled during shutdown");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Client session error");
        }
        finally
        {
            if (session.IsValidated)
            {
                _security.LogDisconnect(session.Uid, session.Pid, session.GetDuration());
            }
        }
    }

    private static void ReleaseSessionGate(SemaphoreSlim activeSessionGate)
    {
        _ = activeSessionGate.Release();
    }

    private readonly record struct ClientSessionAudit(bool IsValidated, uint Uid, int Pid, DateTime SessionStart)
    {
        public static ClientSessionAudit CreatePending() =>
            new(IsValidated: false, 0, 0, DateTime.UtcNow);

        public ClientSessionAudit MarkValidated(uint uid, int pid) =>
            this with
            {
                IsValidated = true,
                Uid = uid,
                Pid = pid,
                SessionStart = DateTime.UtcNow,
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (logOnSuccess)
            {
                Log.LogError(ex, "Failed to clean up socket on exit");
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
        if (Interlocked.Exchange(ref _shutdownRequested, 1) is not 0)
        {
            return;
        }

        Log.Information("Stopping daemon listener...");
        SystemdNotify.Status("Stopping daemon");
        CloseListeningSocket();
    }

    private void CloseListeningSocket()
    {
        var socket = Interlocked.Exchange(ref _socket, value: null);
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
        catch (ObjectDisposedException) { /* Empty */ }
    }
}
