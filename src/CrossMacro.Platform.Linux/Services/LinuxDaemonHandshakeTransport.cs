using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux.Ipc;

namespace CrossMacro.Platform.Linux.Services;

internal static class LinuxDaemonHandshakeTransport
{
    internal readonly record struct ProbeResult(bool Succeeded, bool TimedOut, Exception? Failure)
    {
        public static ProbeResult Success()
        {
            return new(true, false, null);
        }

        public static ProbeResult Failed(Exception? failure = null)
        {
            return new(false, false, failure);
        }

        public static ProbeResult Timeout(Exception? failure = null)
        {
            return new(false, true, failure);
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
            if (opcode == IpcOpCode.Error)
            {
                var message = ReadStringWithinBudget(stream, startedUtc, timeout);
                return ProbeResult.Failed(
                    new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {message}"));
            }

            if (opcode != IpcOpCode.Handshake)
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
                        $"Protocol version mismatch. Daemon: {version}, Client: {IpcProtocol.ProtocolVersion}"));
            }

            return ProbeResult.Success();
        }
        catch (Exception ex) when (IsTimeoutException(ex))
        {
            return ProbeResult.Timeout(ex);
        }
        catch (Exception ex)
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
                if (socketError != SocketError.Success)
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
        catch (Exception ex)
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
        Span<byte> payload = stackalloc byte[sizeof(byte) + sizeof(int)];
        payload[0] = (byte)IpcOpCode.Handshake;
        BinaryPrimitives.WriteInt32LittleEndian(payload[1..], IpcProtocol.ProtocolVersion);
        WriteExactWithinBudget(stream, payload, startedUtc, timeout);
        ConfigureWriteTimeout(stream, startedUtc, timeout);
        stream.Flush();
    }

    private static byte ReadByteWithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        Span<byte> buffer = stackalloc byte[1];
        ReadExactWithinBudget(stream, buffer, startedUtc, timeout);
        return buffer[0];
    }

    private static int ReadInt32WithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadExactWithinBudget(stream, buffer, startedUtc, timeout);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    private static string ReadStringWithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        var byteCount = Read7BitEncodedIntWithinBudget(stream, startedUtc, timeout);
        if (byteCount < 0)
        {
            throw new IOException("Daemon handshake returned a negative string length.");
        }

        if (byteCount == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[byteCount];
        ReadExactWithinBudget(stream, buffer, startedUtc, timeout);
        return Encoding.UTF8.GetString(buffer);
    }

    private static int Read7BitEncodedIntWithinBudget(NetworkStream stream, DateTime startedUtc, TimeSpan timeout)
    {
        var result = 0;
        var shift = 0;

        while (shift < 35)
        {
            var next = ReadByteWithinBudget(stream, startedUtc, timeout);
            result |= (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new IOException("Daemon handshake returned an invalid 7-bit encoded string length.");
    }

    private static void ReadExactWithinBudget(NetworkStream stream, Span<byte> destination, DateTime startedUtc, TimeSpan timeout)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            ConfigureReadTimeout(stream, startedUtc, timeout);
            var read = stream.Read(destination[offset..]);
            if (read <= 0)
            {
                throw new EndOfStreamException("Daemon closed the connection during handshake.");
            }

            offset += read;
        }
    }

    private static void WriteExactWithinBudget(NetworkStream stream, ReadOnlySpan<byte> payload, DateTime startedUtc, TimeSpan timeout)
    {
        ConfigureWriteTimeout(stream, startedUtc, timeout);
        stream.Write(payload);
    }

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
        return socketError == SocketError.TimedOut;
    }

    private static bool IsInProgressConnect(SocketException ex)
    {
        return ex.SocketErrorCode == SocketError.WouldBlock ||
               ex.SocketErrorCode == SocketError.InProgress ||
               ex.SocketErrorCode == SocketError.AlreadyInProgress;
    }

    private static void TryCloseSocket(Socket socket)
    {
        try
        {
            socket.Dispose();
        }
        catch
        {
            // Best effort cleanup for timed out startup probes.
        }
    }
}
