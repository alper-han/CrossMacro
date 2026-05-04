namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using System.Net.Sockets;
using System.Text;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriIpcClientTests
{
    [Fact]
    public void TryNormalizeSocketPath_ShouldAcceptSocketPathInsideRuntimeDirectory()
    {
        var result = NiriIpcClient.TryNormalizeSocketPath(
            "/run/user/1000/niri.wayland-1.sock",
            "/run/user/1000",
            out var normalizedSocketPath);

        Assert.True(result);
        Assert.Equal("/run/user/1000/niri.wayland-1.sock", normalizedSocketPath);
    }

    [Fact]
    public void TryNormalizeSocketPath_ShouldNormalizeRuntimeDirectoryTraversal()
    {
        var result = NiriIpcClient.TryNormalizeSocketPath(
            "/run/user/1000/crossmacro/../niri.sock",
            "/run/user/1000",
            out var normalizedSocketPath);

        Assert.True(result);
        Assert.Equal("/run/user/1000/niri.sock", normalizedSocketPath);
    }

    [Theory]
    [InlineData(null, "/run/user/1000")]
    [InlineData("", "/run/user/1000")]
    [InlineData("/run/user/1000/niri.sock", null)]
    [InlineData("/run/user/1000/niri.sock", "")]
    [InlineData("niri.sock", "/run/user/1000")]
    [InlineData("/tmp/attacker.sock", "/run/user/1000")]
    [InlineData("/run/user/1000evil/niri.sock", "/run/user/1000")]
    [InlineData("/run/user/1000/../1001/niri.sock", "/run/user/1000")]
    public void TryNormalizeSocketPath_ShouldRejectUnsafeSocketPath(string? socketPath, string? runtimeDirectory)
    {
        var result = NiriIpcClient.TryNormalizeSocketPath(socketPath, runtimeDirectory, out var normalizedSocketPath);

        Assert.False(result);
        Assert.Null(normalizedSocketPath);
    }

    [Fact]
    public void Constructor_ShouldNotExposeUnsafeSocketPath()
    {
        using var client = new NiriIpcClient("/tmp/attacker.sock", "/run/user/1000");

        Assert.False(client.IsAvailable);
        Assert.Null(client.SocketPath);
    }

    [Fact]
    public async Task SendRequestAsync_ShouldReadFragmentedNewlineTerminatedResponse()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-niri-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runtimeDirectory);
        var socketPath = Path.Combine(runtimeDirectory, "niri.sock");

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(1);

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptAsync();
            var requestBuffer = new byte[64];
            var requestLength = await accepted.ReceiveAsync(requestBuffer, SocketFlags.None);
            var request = Encoding.UTF8.GetString(requestBuffer, 0, requestLength);
            Assert.Equal("\"Outputs\"\n", request);

            await accepted.SendAsync(Encoding.UTF8.GetBytes("{ \"Ok\": "), SocketFlags.None);
            await accepted.SendAsync(Encoding.UTF8.GetBytes("{ \"Outputs\": {} } }\n"), SocketFlags.None);
        });

        try
        {
            using var client = new NiriIpcClient(socketPath, runtimeDirectory);

            var response = await client.SendRequestAsync("\"Outputs\"");

            Assert.Equal("{ \"Ok\": { \"Outputs\": {} } }", response);
            await serverTask;
        }
        finally
        {
            listener.Close();
            Directory.Delete(runtimeDirectory, recursive: true);
        }
    }
}
