namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


[Collection("EnvironmentVariableSensitive")]
public sealed class HyprlandIpcClientTests
{
    [Fact]
    public async Task WhenHyprlandEnvironmentMissing_ClientShouldBeUnavailableAndReturnNullResponses()
    {
        using var sigScope = new EnvironmentVariableScope("HYPRLAND_INSTANCE_SIGNATURE", value: null);
        using var runtimeScope = new EnvironmentVariableScope("XDG_RUNTIME_DIR", value: null);

        using var client = new HyprlandIpcClient();

        Assert.False(client.IsAvailable);
        Assert.Null(client.SocketPath);
        Assert.Null(await client.SendCommandAsync("cursorpos"));
        Assert.Null(await client.SendCommandAsync([]));
    }

    [Fact]
    public void Constructor_UsesSocketForActiveInstanceSignature()
    {
        var runtimeDirectory = Path.Combine("/tmp", $"crossmacro-hyprland-{Guid.NewGuid():N}");
        var activeInstanceDirectory = Path.Combine(runtimeDirectory, "hypr", "active-instance");
        var inactiveInstanceDirectory = Path.Combine(runtimeDirectory, "hypr", "inactive-instance");

        try
        {
            _ = Directory.CreateDirectory(activeInstanceDirectory);
            _ = Directory.CreateDirectory(inactiveInstanceDirectory);
            var activeSocket = Path.Combine(activeInstanceDirectory, ".socket.sock");
            File.WriteAllText(activeSocket, string.Empty);
            File.WriteAllText(Path.Combine(inactiveInstanceDirectory, ".socket.sock"), string.Empty);
            var environment = default(LinuxEnvironmentSnapshot) with
            {
                HyprlandInstanceSignature = "active-instance",
                RuntimeDir = runtimeDirectory,
            };

            using var client = new HyprlandIpcClient(environment);

            Assert.True(client.IsAvailable);
            Assert.Equal(activeSocket, client.SocketPath);
        }
        finally
        {
            if (Directory.Exists(runtimeDirectory))
            {
                Directory.Delete(runtimeDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SendCommandAsync_ReadsFragmentedResponseUntilPeerCloses()
    {
        var runtimeDirectory = Path.Combine("/tmp", $"crossmacro-hyprland-{Guid.NewGuid():N}");
        var instanceDirectory = Path.Combine(runtimeDirectory, "hypr", "active-instance");
        _ = Directory.CreateDirectory(instanceDirectory);
        var socketPath = Path.Combine(instanceDirectory, ".socket.sock");

        try
        {
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(backlog: 1);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var serverTask = ServeFragmentedResponseAsync(listener, timeout.Token);
            var environment = default(LinuxEnvironmentSnapshot) with
            {
                HyprlandInstanceSignature = "active-instance",
                RuntimeDir = runtimeDirectory,
            };

            using var client = new HyprlandIpcClient(environment);
            var response = await client.SendCommandAsync("cursorpos", timeout.Token);
            var command = await serverTask;

            Assert.Equal("cursorpos", command);
            Assert.Equal("120.5, 240.25", response);
        }
        finally
        {
            if (Directory.Exists(runtimeDirectory))
            {
                Directory.Delete(runtimeDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("../other-instance")]
    [InlineData("nested/other-instance")]
    public void Constructor_RejectsInvalidInstanceSignature(string instanceSignature)
    {
        var environment = default(LinuxEnvironmentSnapshot) with
        {
            HyprlandInstanceSignature = instanceSignature,
            RuntimeDir = "/tmp",
        };

        using var client = new HyprlandIpcClient(environment);

        Assert.False(client.IsAvailable);
        Assert.Null(client.SocketPath);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }

    private static async Task<string> ServeFragmentedResponseAsync(
        Socket listener,
        CancellationToken cancellationToken)
    {
        using var connection = await listener.AcceptAsync(cancellationToken);
        var commandBuffer = new byte[32];
        int received = await connection.ReceiveAsync(commandBuffer, SocketFlags.None, cancellationToken);
        await connection.SendAsync("120.5, "u8.ToArray(), SocketFlags.None, cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        await connection.SendAsync("240.25"u8.ToArray(), SocketFlags.None, cancellationToken);
        connection.Shutdown(SocketShutdown.Send);
        return Encoding.UTF8.GetString(commandBuffer, 0, received);
    }
}
