
namespace CrossMacro.Platform.Linux.Services.Runtime;

/// <summary>One-shot connect + handshake used to decide whether the daemon is usable.</summary>
internal static class LinuxDaemonHandshakeTransport
{
    public static async Task<LinuxDaemonHandshakeProbeResult> ProbeAsync(
        string socketPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(timeout);

            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), budget.Token).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: false);
            await using (stream.ConfigureAwait(false))
            {
                await IpcHandshakeExchange.PerformAsync(stream, budget.Token).ConfigureAwait(false);
            }

            return LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            return LinuxDaemonHandshakeProbeResult.Failed(
                socketPath, timeout, LinuxDaemonHandshakeStatus.Timeout, ex.Message, ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Failed(socketPath, timeout, ex);
        }
    }

    internal static LinuxDaemonHandshakeProbeResult Failed(string socketPath, TimeSpan timeout, Exception? failure) =>
        LinuxDaemonHandshakeProbeResult.Failed(socketPath, timeout, MapFailure(failure), failure?.Message, failure);

    internal static LinuxDaemonHandshakeStatus MapFailure(Exception? failure)
    {
        return failure switch
        {
            null => LinuxDaemonHandshakeStatus.UnexpectedError,
            IpcClientException ipcClientException => MapIpcFailure(ipcClientException.Reason),
            UnauthorizedAccessException => LinuxDaemonHandshakeStatus.PermissionDenied,
            TimeoutException => LinuxDaemonHandshakeStatus.Timeout,
            FileNotFoundException or DirectoryNotFoundException => LinuxDaemonHandshakeStatus.MissingSocket,
            SocketException socketException => MapSocketError(socketException.SocketErrorCode),
            IOException { InnerException: SocketException inner } => MapSocketError(inner.SocketErrorCode),
            IOException ioException when ioException.Message.Contains("not a socket", StringComparison.OrdinalIgnoreCase) =>
                LinuxDaemonHandshakeStatus.WrongSocketType,
            _ => LinuxDaemonHandshakeStatus.UnexpectedError,
        };
    }

    private static LinuxDaemonHandshakeStatus MapIpcFailure(IpcClientFailureReason reason) =>
        reason switch
        {
            IpcClientFailureReason.SocketNotFound => LinuxDaemonHandshakeStatus.MissingSocket,
            IpcClientFailureReason.ConnectFailed => LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale,
            IpcClientFailureReason.PermissionDenied => LinuxDaemonHandshakeStatus.PermissionDenied,
            IpcClientFailureReason.HandshakeFailed or IpcClientFailureReason.SimulationRejected => LinuxDaemonHandshakeStatus.HandshakeRejected,
            IpcClientFailureReason.ProtocolMismatch => LinuxDaemonHandshakeStatus.ProtocolMismatch,
            IpcClientFailureReason.Timeout => LinuxDaemonHandshakeStatus.Timeout,
            IpcClientFailureReason.IntegrityMismatch => LinuxDaemonHandshakeStatus.UnexpectedError,
            _ => LinuxDaemonHandshakeStatus.UnexpectedError,
        };

    private static LinuxDaemonHandshakeStatus MapSocketError(SocketError error)
    {
        if (error is SocketError.AccessDenied)
        {
            return LinuxDaemonHandshakeStatus.PermissionDenied;
        }

        if (error is SocketError.TimedOut)
        {
            return LinuxDaemonHandshakeStatus.Timeout;
        }

        return error is SocketError.ConnectionRefused
            ? LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale
            : LinuxDaemonHandshakeStatus.UnexpectedError;
    }
}
