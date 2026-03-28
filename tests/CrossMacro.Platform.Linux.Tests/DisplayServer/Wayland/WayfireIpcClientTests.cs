namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public class WayfireIpcClientTests
{
    [Fact]
    public void Constructor_ShouldPreferWayfireSocketEnvironmentVariable()
    {
        const string envSocket = "/tmp/wayfire-env.socket";

        using var client = new WayfireIpcClient(
            key => key == "WAYFIRE_SOCKET" ? envSocket : null,
            path => path == envSocket,
            _ => false,
            (_, _) => [],
            path => path == envSocket);

        Assert.True(client.IsAvailable);
        Assert.Equal(envSocket, client.SocketPath);
    }

    [Fact]
    public void Constructor_ShouldFallbackToRuntimeAndTmpCandidates()
    {
        const string runtimeDir = "/run/user/1000";
        const string tmpSocket = "/tmp/wayfire-wayland-2.socket";
        const string runtimeSocket = "/run/user/1000/wayfire-wayland-1.socket";

        using var client = new WayfireIpcClient(
            key => key switch
            {
                "WAYFIRE_SOCKET" => null,
                "XDG_RUNTIME_DIR" => runtimeDir,
                _ => null
            },
            path => path == runtimeSocket || path == tmpSocket,
            directory => directory is runtimeDir or "/tmp",
            (directory, _) => directory switch
            {
                runtimeDir => ["/run/user/1000/wayfire-wayland-3.socket", runtimeSocket],
                "/tmp" => [tmpSocket],
                _ => []
            },
            path => path == runtimeSocket || path == tmpSocket);

        Assert.True(client.IsAvailable);
        Assert.Equal(runtimeSocket, client.SocketPath);
    }

    [Fact]
    public void Constructor_ShouldSkipStaleSocketAndPickConnectableCandidate()
    {
        const string runtimeDir = "/run/user/1000";
        const string staleSocket = "/run/user/1000/wayfire-wayland-1.socket";
        const string liveSocket = "/run/user/1000/wayfire-wayland-2.socket";

        using var client = new WayfireIpcClient(
            key => key switch
            {
                "WAYFIRE_SOCKET" => null,
                "XDG_RUNTIME_DIR" => runtimeDir,
                _ => null
            },
            path => path is staleSocket or liveSocket,
            directory => directory == runtimeDir,
            (_, _) => [staleSocket, liveSocket],
            path => path == liveSocket);

        Assert.True(client.IsAvailable);
        Assert.Equal(liveSocket, client.SocketPath);
    }

    [Fact]
    public void Constructor_ShouldFallbackWhenWayfireSocketEnvIsStale()
    {
        const string runtimeDir = "/run/user/1000";
        const string staleEnvSocket = "/tmp/wayfire-wayland-stale.socket";
        const string liveRuntimeSocket = "/run/user/1000/wayfire-wayland-3.socket";

        using var client = new WayfireIpcClient(
            key => key switch
            {
                "WAYFIRE_SOCKET" => staleEnvSocket,
                "XDG_RUNTIME_DIR" => runtimeDir,
                _ => null
            },
            path => path is staleEnvSocket or liveRuntimeSocket,
            directory => directory == runtimeDir,
            (_, _) => [liveRuntimeSocket],
            path => path == liveRuntimeSocket);

        Assert.True(client.IsAvailable);
        Assert.Equal(liveRuntimeSocket, client.SocketPath);
    }

    [Fact]
    public async Task SendRequestAsync_ShouldUseLengthPrefixedJsonProtocol()
    {
        using var tempDirectory = new TempDirectory();
        var socketPath = Path.Combine(tempDirectory.Path, "wayfire-wayland-test.socket");

        using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        server.Bind(new UnixDomainSocketEndPoint(socketPath));
        server.Listen(1);

        var serverTask = Task.Run(async () =>
        {
            using var connection = await server.AcceptAsync();
            var requestPayload = await ReadFramedMessageAsync(connection);

            var responsePayload = "{\"result\":\"ok\"}";
            await WriteFramedMessageAsync(connection, responsePayload);

            return requestPayload;
        });

        using var client = new WayfireIpcClient(
            key => key == "WAYFIRE_SOCKET" ? socketPath : null,
            path => path == socketPath,
            _ => false,
            (_, _) => [],
            path => path == socketPath);

        var response = await client.SendRequestAsync("window-rules/get_cursor_position");
        var requestPayload = await serverTask;

        Assert.Equal("{\"result\":\"ok\"}", response);

        using var requestDoc = JsonDocument.Parse(requestPayload);
        Assert.Equal("window-rules/get_cursor_position", requestDoc.RootElement.GetProperty("method").GetString());
        Assert.True(requestDoc.RootElement.TryGetProperty("data", out var dataElement));
        Assert.Equal(JsonValueKind.Object, dataElement.ValueKind);
    }

    private static async Task<string> ReadFramedMessageAsync(Socket socket)
    {
        var header = new byte[4];
        await ReadExactAsync(socket, header);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);

        var payload = new byte[payloadLength];
        await ReadExactAsync(socket, payload);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteFramedMessageAsync(Socket socket, string payload)
    {
        var data = Encoding.UTF8.GetBytes(payload);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);

        await WriteAllAsync(socket, header);
        await WriteAllAsync(socket, data);
    }

    private static async Task ReadExactAsync(Socket socket, byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int chunk = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None);
            if (chunk <= 0)
            {
                throw new IOException("Unexpected EOF.");
            }

            read += chunk;
        }
    }

    private static async Task WriteAllAsync(Socket socket, byte[] buffer)
    {
        int written = 0;
        while (written < buffer.Length)
        {
            int chunk = await socket.SendAsync(buffer.AsMemory(written), SocketFlags.None);
            if (chunk <= 0)
            {
                throw new IOException("Failed to write payload.");
            }

            written += chunk;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
