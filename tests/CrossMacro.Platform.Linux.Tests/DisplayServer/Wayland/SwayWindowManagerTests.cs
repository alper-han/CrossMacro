namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class SwayWindowManagerTests
{
    [Fact]
    public async Task GetActiveWindowAsync_ShouldMapFocusedNestedNode()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses =
            {
                [4] = "{\"id\":1,\"type\":\"root\",\"nodes\":[{\"id\":2,\"type\":\"workspace\",\"name\":\"main\",\"nodes\":[{\"id\":3,\"type\":\"con\",\"name\":\"Editor\",\"app_id\":\"org.example\",\"pid\":0,\"focused\":true,\"fullscreen_mode\":1,\"sticky\":true,\"rect\":{\"x\":10,\"y\":20,\"width\":800,\"height\":600}}]}]}",
            },
        };
        var manager = new SwayWindowManager(client);

        var result = await manager.GetActiveWindowAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("3", result.Address);
        Assert.Equal("Editor", result.Title);
        Assert.Equal("org.example", result.Class);
        Assert.Equal(-1, result.Pid);
        Assert.Equal(string.Empty, result.ProcessName);
        Assert.True(result.IsFocused);
        Assert.True(result.IsFullscreen);
        Assert.True(result.IsPinned);
        Assert.Equal(10, result.X);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public async Task GetWindowsAsync_ShouldTraverseWorkspaceAndFloatingNodes()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses =
            {
                [4] = "{\"id\":1,\"type\":\"root\",\"nodes\":[{\"id\":2,\"type\":\"workspace\",\"name\":\"main\",\"nodes\":[{\"id\":3,\"type\":\"con\",\"name\":\"Tiled\",\"window_properties\":{\"class\":\"XWayland\"}}],\"floating_nodes\":[{\"id\":4,\"type\":\"floating_con\",\"app_id\":\"org.float\",\"name\":\"Floating\"}]}]}",
            },
        };
        var manager = new SwayWindowManager(client);

        var result = await manager.GetWindowsAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("main", result[0].Workspace);
        Assert.Equal("XWayland", result[0].Class);
        Assert.Equal("main", result[1].Workspace);
        Assert.Equal("org.float", result[1].Class);
        Assert.True(result[1].IsFloating);
    }

    [Fact]
    public async Task CommandsAndWorkspace_ShouldPreservePayloadsAndSuccessMapping()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses =
            {
                [0] = "[{\"success\":true}]",
                [1] = "[{\"name\":\"main\",\"focused\":true}]",
            },
        };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.FocusWindowByTitleAsync("A\"B", CancellationToken.None));
        Assert.Equal("[title=\"A\\\"B\"] focus", Assert.Single(client.Requests).Payload);
        Assert.Equal("main", await manager.GetActiveWorkspaceAsync(CancellationToken.None));
        Assert.Equal((uint)1, client.Requests[1].Type);
    }

    [Fact]
    public async Task InvalidAndMalformedResponses_ShouldFailClosedWithoutThrowing()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses =
            {
                [0] = "not-json",
                [1] = "not-json",
                [4] = null,
            },
        };
        var manager = new SwayWindowManager(client);

        Assert.False(await manager.FocusWindowByTitleAsync(" ", CancellationToken.None));
        Assert.False(await manager.FocusWindowByTitleAsync("Title", CancellationToken.None));
        Assert.Null(await manager.GetActiveWorkspaceAsync(CancellationToken.None));
        Assert.Null(await manager.GetActiveWindowAsync(CancellationToken.None));
        Assert.Empty(await manager.GetWindowsAsync(CancellationToken.None));
        Assert.Equal(4, client.Requests.Count);
    }

    private sealed class FakeSwayIpcClient : ISwayIpcClient
    {
        public Dictionary<uint, string?> Responses { get; init; } = [];

        public List<(uint Type, string Payload)> Requests { get; } = [];

        public bool IsAvailable => true;

        public string? SocketPath => null;

        public Task<string?> SendRequestAsync(uint type, string payload = "", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((type, payload));
            return Task.FromResult(Responses.GetValueOrDefault(type));
        }

        public void Dispose() { }
    }
}
