namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


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
        var socketPath = TestSocketPaths.CreateShort("cm-niri");

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(1);

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptAsync(CancellationToken.None);
            var requestBuffer = new byte[64];
            var requestLength = await accepted.ReceiveAsync(requestBuffer, SocketFlags.None);
            var request = Encoding.UTF8.GetString(requestBuffer, 0, requestLength);
            Assert.Equal("\"Outputs\"\n", request);

            _ = await accepted.SendAsync(Encoding.UTF8.GetBytes("{ \"Ok\": "), SocketFlags.None);
            _ = await accepted.SendAsync(Encoding.UTF8.GetBytes("{ \"Outputs\": {} } }\n"), SocketFlags.None);
        }, CancellationToken.None);

        try
        {
            using var client = new NiriIpcClient(socketPath, Path.GetDirectoryName(socketPath)!);

            var response = await client.SendRequestAsync("\"Outputs\"", CancellationToken.None);

            Assert.Equal("{ \"Ok\": { \"Outputs\": {} } }", response);
            await serverTask;
        }
        finally
        {
            listener.Close();
            File.Delete(socketPath);
        }
    }

    [Fact]
    public async Task SendRequestAsync_ShouldTransmitCompleteLargeRequestBeforeReadingResponse()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-niri");
        var requestJson = $"\"{new string('x', 1024 * 1024)}\"";

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(1);

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptAsync(CancellationToken.None);
            using var requestBuffer = new MemoryStream();
            var receiveBuffer = new byte[8192];
            while (true)
            {
                var received = await accepted.ReceiveAsync(receiveBuffer, SocketFlags.None, CancellationToken.None);
                if (received <= 0)
                {
                    break;
                }

                await requestBuffer.WriteAsync(receiveBuffer.AsMemory(0, received), CancellationToken.None);
                if (receiveBuffer[received - 1] == (byte)'\n')
                {
                    break;
                }
            }

            Assert.Equal(requestJson + "\n", Encoding.UTF8.GetString(requestBuffer.ToArray()));
            _ = await accepted.SendAsync(Encoding.UTF8.GetBytes("{}\n"), SocketFlags.None, CancellationToken.None);
        }, CancellationToken.None);

        try
        {
            using var client = new NiriIpcClient(socketPath, Path.GetDirectoryName(socketPath)!);

            var response = await client.SendRequestAsync(requestJson, CancellationToken.None);

            Assert.Equal("{}", response);
            await serverTask;
        }
        finally
        {
            listener.Close();
            File.Delete(socketPath);
        }
    }
}
