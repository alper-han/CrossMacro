namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class SwayPositionProviderTests
{
    [Fact]
    public void TryParseDesktopBounds_ShouldPreserveNegativeLogicalOrigin()
    {
        const string response = """
            [
              { "active": true, "rect": { "x": -1920, "y": -200, "width": 1920, "height": 1080 } },
              { "active": true, "rect": { "x": 0, "y": 0, "width": 2560, "height": 1440 } },
              { "active": false, "rect": { "x": 9999, "y": 9999, "width": 100, "height": 100 } }
            ]
            """;

        var parsed = SwayPositionProvider.TryParseDesktopBounds(response, out var bounds);

        Assert.True(parsed);
        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{ \"active\": false, \"rect\": { \"x\": 0, \"y\": 0, \"width\": 1920, \"height\": 1080 } }]")]
    public void TryParseDesktopBounds_ShouldRejectUnavailableOutputs(string response)
    {
        var parsed = SwayPositionProvider.TryParseDesktopBounds(response, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseDesktopBounds_ShouldRejectOverflowingOutputExtent()
    {
        const string response =
            "[{ \"active\": true, \"rect\": { \"x\": 2147483640, \"y\": 0, \"width\": 100, \"height\": 100 } }]";

        Assert.False(SwayPositionProvider.TryParseDesktopBounds(response, out _));
    }

    [Fact]
    public void Dispose_ShouldDisposeOwnedIpcClient()
    {
        var ipcClient = new RecordingSwayIpcClient();
        using var provider = new SwayPositionProvider(ipcClient);

        provider.Dispose();

        Assert.Equal(1, ipcClient.DisposeCallCount);
    }

    private sealed class RecordingSwayIpcClient : ISwayIpcClient
    {
        public bool IsAvailable => false;
        public string? SocketPath => null;
        public int DisposeCallCount { get; private set; }

        public Task<string?> SendRequestAsync(
            uint type,
            string payload = "",
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }
}
