using CrossMacro.TestInfrastructure;

namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{
    private sealed class UnixSocketPair : IAsyncDisposable
    {
        private readonly Socket _listener;
        private readonly string _path;
        public Socket Client { get; }
        public Socket Server { get; }

        private UnixSocketPair(Socket listener, string path, Socket client, Socket server)
        {
            _listener = listener;
            _path = path;
            Client = client;
            Server = server;
        }

        public static async Task<UnixSocketPair> CreateAsync()
        {
            var path = TestSocketPaths.CreateShort("cm-session");
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);

            var acceptTask = listener.AcceptAsync(CancellationToken.None).AsTask();

            var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(path), CancellationToken.None);
            var server = await acceptTask;

            return new UnixSocketPair(listener, path, client, server);
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            Server.Dispose();
            _listener.Dispose();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return ValueTask.CompletedTask;
        }
    }

    private static Task StartSessionOnBackgroundThreadAsync(
        SessionHandler handler,
        Socket server,
        uint uid,
        int pid,
        CancellationToken token)
    {
        return Task.Factory.StartNew(
            () => handler.RunAsync(server, uid, pid, token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private static void CompleteHandshake(BinaryReader reader, BinaryWriter writer)
    {
        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());
    }

    private static void SendStartCaptureCommand(BinaryReader reader, BinaryWriter writer, int requestId)
    {
        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(requestId);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(requestId, reader.ReadInt32());
    }

    private static void AssertNoMessageAvailable(NetworkStream stream, BinaryReader reader, TimeSpan timeout)
    {
        var previousTimeout = stream.ReadTimeout;
        stream.ReadTimeout = (int)timeout.TotalMilliseconds;

        try
        {
            var exception = Assert.Throws<IOException>(() => reader.ReadByte());
            Assert.True(
                exception.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut } ||
                exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase),
                $"Expected a read timeout while the session remained open, but got: {exception}");
        }
        finally
        {
            stream.ReadTimeout = previousTimeout;
        }
    }

    private static async Task AssertRemoteClosedAsync(NetworkStream stream, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var buffer = new byte[1];

        try
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cts.Token);
            Assert.Equal(0, bytesRead);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for the session handler to close the client stream.", ex);
        }
        catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.NotConnected })
        {
            _ = ex;
        }
        catch (ObjectDisposedException ex)
        {
            _ = ex;
        }
    }
}
