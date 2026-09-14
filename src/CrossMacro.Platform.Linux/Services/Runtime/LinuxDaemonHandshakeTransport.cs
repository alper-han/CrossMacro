
namespace CrossMacro.Platform.Linux.Services.Runtime;

internal static class LinuxDaemonHandshakeTransport
{
    internal readonly record struct ProbeResult(bool Succeeded, bool TimedOut, Exception? Failure)
    {
        public static ProbeResult Success()
        {
            return new(Succeeded: true, TimedOut: false, Failure: null);
        }

        public static ProbeResult Failed(Exception? failure = null)
        {
            return new(Succeeded: false, TimedOut: false, failure);
        }

        public static ProbeResult Timeout(Exception? failure = null)
        {
            return new(Succeeded: false, TimedOut: true, failure);
        }
    }

    public static ProbeResult ProbeWithinBudget(string socketPath, TimeSpan timeout)
    {
        try
        {
            var startedUtc = DateTime.UtcNow;
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            var connectResult = ConnectWithinBudget(socket, endpoint, startedUtc, timeout);
            if (connectResult.TimedOut)
            {
                TryCloseSocket(socket);
                return connectResult;
            }

            if (!connectResult.Succeeded)
            {
                return connectResult;
            }

            using var stream = new NetworkStream(socket, ownsSocket: false);
            WriteHandshakeRequest(stream, startedUtc, timeout);

            var opcode = (IpcOpCode)ReadByteWithinBudget(stream, startedUtc, timeout);
            if (opcode is IpcOpCode.Error)
            {
                var message = ReadStringWithinBudget(stream, startedUtc, timeout);
                return ProbeResult.Failed(
                    new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {message}"));
            }

            if (opcode is not IpcOpCode.Handshake)
            {
                return ProbeResult.Failed(
                    new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Unexpected handshake opcode: {opcode}"));
            }

            var version = ReadInt32WithinBudget(stream, startedUtc, timeout);
            if (version != IpcProtocol.ProtocolVersion)
            {
                return ProbeResult.Failed(
                    new IpcClientException(
                        IpcClientFailureReason.ProtocolMismatch,
                        $"Protocol version mismatch. Daemon: {version.ToString(CultureInfo.InvariantCulture)}, Client: {IpcProtocol.ProtocolVersion.ToString(CultureInfo.InvariantCulture)}"));
            }

            return ProbeResult.Success();
        }
        catch (Exception ex) when (IsTimeoutException(ex))
        {
            return ProbeResult.Timeout(ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ProbeResult.Failed(ex);
        }
    }

    public static async Task<ProbeResult> ProbeWithinBudgetAsync(
        string socketPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await socket.ConnectAsync(endpoint, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return ProbeResult.Timeout(ex);
            }

            var stream = new NetworkStream(socket, ownsSocket: false);
            await using var streamDisposal = stream.ConfigureAwait(false);
            await WriteHandshakeRequestAsync(stream, timeoutCts.Token).ConfigureAwait(false);

            var opcode = (IpcOpCode)await ReadByteAsync(stream, timeoutCts.Token).ConfigureAwait(false);
            if (opcode is IpcOpCode.Error)
            {
                var message = await ReadStringAsync(stream, timeoutCts.Token).ConfigureAwait(false);
                return ProbeResult.Failed(
                    new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {message}"));
            }

            if (opcode is not IpcOpCode.Handshake)
            {
                return ProbeResult.Failed(
                    new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Unexpected handshake opcode: {opcode}"));
            }

            var version = await ReadInt32Async(stream, timeoutCts.Token).ConfigureAwait(false);
            if (version != IpcProtocol.ProtocolVersion)
            {
                return ProbeResult.Failed(
                    new IpcClientException(
                        IpcClientFailureReason.ProtocolMismatch,
                        $"Protocol version mismatch. Daemon: {version.ToString(CultureInfo.InvariantCulture)}, Client: {IpcProtocol.ProtocolVersion.ToString(CultureInfo.InvariantCulture)}"));
            }

            return ProbeResult.Success();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeResult.Timeout(ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ProbeResult.Failed(ex);
        }
    }

    private static ProbeResult ConnectWithinBudget(Socket socket, EndPoint endpoint, DateTime startedUtc, TimeSpan timeout)
    {
        try
        {
            socket.Blocking = false;

            try
            {
                socket.Connect(endpoint);
            }
            catch (SocketException ex) when (IsInProgressConnect(ex))
            {
                var remainingBudget = GetRemainingBudget(startedUtc, timeout);
                if (remainingBudget <= TimeSpan.Zero)
                {
                    return ProbeResult.Timeout(ex);
                }

                if (!socket.Poll(ToPollMicroseconds(remainingBudget), SelectMode.SelectWrite))
                {
                    return ProbeResult.Timeout(ex);
                }

                var socketErrorOption = socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                var socketError = socketErrorOption is int socketErrorValue
                    ? (SocketError)socketErrorValue
                    : SocketError.SocketError;
                if (socketError is not SocketError.Success)
                {
                    return IsSocketTimeout(socketError)
                        ? ProbeResult.Timeout(new SocketException((int)socketError))
                        : ProbeResult.Failed(new SocketException((int)socketError));
                }
            }

            socket.Blocking = true;
            return ProbeResult.Success();
        }
        catch (Exception ex) when (IsTimeoutException(ex))
        {
            return ProbeResult.Timeout(ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ProbeResult.Failed(ex);
        }
    }

    private static TimeSpan GetRemainingBudget(DateTime startedUtc, TimeSpan timeout)
    {
        var elapsed = DateTime.UtcNow - startedUtc;
        var remaining = timeout - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static TimeSpan GetRemainingBudgetOrThrow(DateTime startedUtc, TimeSpan timeout)
    {
        var remaining = GetRemainingBudget(startedUtc, timeout);
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("Daemon handshake probe budget exhausted.");
        }

        return remaining;
    }

    private static int ToSocketTimeoutMilliseconds(TimeSpan timeout)
    {
        var milliseconds = (int)Math.Ceiling(timeout.TotalMilliseconds);
        return Math.Max(1, milliseconds);
    }

    private static int ToPollMicroseconds(TimeSpan timeout)
    {
        const int microsecondsPerMillisecond = 1000;
        var milliseconds = ToSocketTimeoutMilliseconds(timeout);
        return checked(milliseconds * microsecondsPerMillisecond);
    }

    private static void WriteHandshakeRequest(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        ConfigureWriteTimeout(stream, startedUtc, timeout);
        stream.Write(IpcHandshakeWireCodec.CreateRequest());
        ConfigureWriteTimeout(stream, startedUtc, timeout);
        stream.Flush();
    }

    private static async Task WriteHandshakeRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(IpcHandshakeWireCodec.CreateRequest(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static byte ReadByteWithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout) =>
        IpcHandshakeWireCodec.ReadByte(stream, () => ConfigureReadTimeout(stream, startedUtc, timeout));

    private static Task<byte> ReadByteAsync(NetworkStream stream, CancellationToken cancellationToken) =>
        IpcHandshakeWireCodec.ReadByteAsync(stream, cancellationToken);

    private static int ReadInt32WithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout) =>
        IpcHandshakeWireCodec.ReadInt32(stream, () => ConfigureReadTimeout(stream, startedUtc, timeout));

    private static Task<int> ReadInt32Async(NetworkStream stream, CancellationToken cancellationToken) =>
        IpcHandshakeWireCodec.ReadInt32Async(stream, cancellationToken);

    private static string ReadStringWithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout) =>
        IpcHandshakeWireCodec.ReadString(stream, () => ConfigureReadTimeout(stream, startedUtc, timeout));

    private static Task<string> ReadStringAsync(NetworkStream stream, CancellationToken cancellationToken) =>
        IpcHandshakeWireCodec.ReadStringAsync(stream, cancellationToken);

    private static void ConfigureReadTimeout(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        stream.ReadTimeout = ToSocketTimeoutMilliseconds(GetRemainingBudgetOrThrow(startedUtc, timeout));
    }

    private static void ConfigureWriteTimeout(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        stream.WriteTimeout = ToSocketTimeoutMilliseconds(GetRemainingBudgetOrThrow(startedUtc, timeout));
    }

    private static bool IsTimeoutException(Exception ex)
    {
        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is IOException ioException)
        {
            return IsSocketTimeout(ioException.InnerException as SocketException) ||
                   ioException.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
        }

        if (ex is SocketException socketEx && IsSocketTimeout(socketEx))
        {
            return true;
        }

        return false;
    }

    internal static LinuxDaemonHandshakeStatus MapFailure(Exception? failure)
    {
        if (failure is null)
        {
            return LinuxDaemonHandshakeStatus.UnexpectedError;
        }

        if (failure is IpcClientException ipcClientException)
        {
            return ipcClientException.Reason switch
            {
                IpcClientFailureReason.SocketNotFound => LinuxDaemonHandshakeStatus.MissingSocket,
                IpcClientFailureReason.ConnectFailed => LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale,
                IpcClientFailureReason.PermissionDenied => LinuxDaemonHandshakeStatus.PermissionDenied,
                IpcClientFailureReason.HandshakeFailed => LinuxDaemonHandshakeStatus.HandshakeRejected,
                IpcClientFailureReason.ProtocolMismatch => LinuxDaemonHandshakeStatus.ProtocolMismatch,
                IpcClientFailureReason.Timeout => LinuxDaemonHandshakeStatus.Timeout,
                IpcClientFailureReason.SimulationRejected => LinuxDaemonHandshakeStatus.HandshakeRejected,
                IpcClientFailureReason.IntegrityMismatch => LinuxDaemonHandshakeStatus.UnexpectedError,
                _ => LinuxDaemonHandshakeStatus.UnexpectedError,
            };
        }

        return failure switch
        {
            UnauthorizedAccessException => LinuxDaemonHandshakeStatus.PermissionDenied,
            SocketException socketException when socketException.SocketErrorCode is SocketError.AccessDenied => LinuxDaemonHandshakeStatus.PermissionDenied,
            FileNotFoundException => LinuxDaemonHandshakeStatus.MissingSocket,
            DirectoryNotFoundException => LinuxDaemonHandshakeStatus.MissingSocket,
            TimeoutException => LinuxDaemonHandshakeStatus.Timeout,
            IOException ioException when ioException.Message.Contains("not a socket", StringComparison.OrdinalIgnoreCase) => LinuxDaemonHandshakeStatus.WrongSocketType,
            _ => LinuxDaemonHandshakeStatus.UnexpectedError,
        };
    }

    private static bool IsSocketTimeout(SocketException? ex)
    {
        if (ex is null)
        {
            return false;
        }

        return IsSocketTimeout(ex.SocketErrorCode) ||
               ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSocketTimeout(SocketError socketError)
    {
        return socketError is SocketError.TimedOut;
    }

    private static bool IsInProgressConnect(SocketException ex)
    {
        return ex.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress or SocketError.AlreadyInProgress;
    }

    private static void TryCloseSocket(Socket socket)
    {
        try
        {
            socket.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best effort cleanup for timed out startup probes.
        }
    }
}
