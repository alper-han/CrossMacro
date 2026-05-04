namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

[Collection("EnvironmentVariableSensitive")]
public class WayfireIpcClientTests
{
    private static readonly TimeSpan SocketOperationTimeout = TimeSpan.FromSeconds(2);

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
        const string runtimeStaleSocket = "/run/user/1000/wayfire-wayland-1.socket";
        var tempDir = Path.GetTempPath();
        var tmpSocket = Path.Combine(tempDir, "wayfire-wayland-2.socket");

        var callOrder = new List<string>();
        using var client = new WayfireIpcClient(
            key => key switch
            {
                "WAYFIRE_SOCKET" => null,
                "XDG_RUNTIME_DIR" => runtimeDir,
                _ => null
            },
            (string path) => path == runtimeStaleSocket || path == tmpSocket,
            (string directory) => directory == runtimeDir || directory == tempDir,
            (string directory, string _) =>
            {
                if (directory == runtimeDir)
                {
                    return new[] { runtimeStaleSocket };
                }

                if (directory == tempDir)
                {
                    return new[] { tmpSocket };
                }

                return [];
            },
            (string path) =>
            {
                callOrder.Add($"connect:{path}");
                return path == tmpSocket;
            });

        Assert.True(client.IsAvailable);
        Assert.Equal(tmpSocket, client.SocketPath);
        Assert.Equal(
            [
                $"connect:{runtimeStaleSocket}",
                $"connect:{tmpSocket}"
            ],
            callOrder);
    }

    [Fact]
    public void Constructor_ShouldFallbackToTempCandidatesWhenRuntimeDirMissing()
    {
        var tempDir = Path.GetTempPath();
        var tmpSocket = Path.Combine(tempDir, "wayfire-wayland-3.socket");

        var checkedDirectories = new List<string>();

        using var client = new WayfireIpcClient(
            key => key switch
            {
                "WAYFIRE_SOCKET" => null,
                "XDG_RUNTIME_DIR" => null,
                _ => null
            },
            (string path) => path == tmpSocket,
            (string directory) => directory == tempDir,
            (string directory, string _) =>
            {
                checkedDirectories.Add(directory);
                return directory == tempDir ? new[] { tmpSocket } : [];
            },
            (string path) => path == tmpSocket);

        Assert.True(client.IsAvailable);
        Assert.Equal(tmpSocket, client.SocketPath);
        Assert.Equal([tempDir], checkedDirectories);
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
        var socketPath = $"/tmp/cm-wf-{Guid.NewGuid():N}.sock";

        using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            server.Bind(new UnixDomainSocketEndPoint(socketPath));
            server.Listen(1);

            using var timeoutCts = new CancellationTokenSource(SocketOperationTimeout);
            var serverTask = Task.Run(async () =>
            {
                using var connection = await server.AcceptAsync(timeoutCts.Token);
                var requestPayload = await ReadFramedMessageAsync(connection, timeoutCts.Token);

                var responsePayload = "{\"result\":\"ok\"}";
                await WriteFramedMessageAsync(connection, responsePayload, timeoutCts.Token);

                return requestPayload;
            });

            using var client = new WayfireIpcClient(
                key => key == "WAYFIRE_SOCKET" ? socketPath : null,
                path => path == socketPath,
                _ => false,
                (_, _) => [],
                path => path == socketPath);

            var response = await client.SendRequestAsync("window-rules/get_cursor_position").WaitAsync(SocketOperationTimeout);
            var requestPayload = await serverTask.WaitAsync(SocketOperationTimeout);

            Assert.Equal("{\"result\":\"ok\"}", response);

            using var requestDoc = JsonDocument.Parse(requestPayload);
            Assert.Equal("window-rules/get_cursor_position", requestDoc.RootElement.GetProperty("method").GetString());
            Assert.True(requestDoc.RootElement.TryGetProperty("data", out var dataElement));
            Assert.Equal(JsonValueKind.Object, dataElement.ValueKind);
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    private static async Task<string> ReadFramedMessageAsync(Socket socket, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(socket, header, cancellationToken);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);

        var payload = new byte[payloadLength];
        await ReadExactAsync(socket, payload, cancellationToken);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteFramedMessageAsync(Socket socket, string payload, CancellationToken cancellationToken)
    {
        var data = Encoding.UTF8.GetBytes(payload);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);

        await WriteAllAsync(socket, header, cancellationToken);
        await WriteAllAsync(socket, data, cancellationToken);
    }

    private static async Task ReadExactAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int chunk = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None, cancellationToken);
            if (chunk <= 0)
            {
                throw new IOException("Unexpected EOF.");
            }

            read += chunk;
        }
    }

    private static async Task WriteAllAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        int written = 0;
        while (written < buffer.Length)
        {
            int chunk = await socket.SendAsync(buffer.AsMemory(written), SocketFlags.None, cancellationToken);
            if (chunk <= 0)
            {
                throw new IOException("Failed to write payload.");
            }

            written += chunk;
        }
    }
}
