
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class NiriWindowManagerTests
{
    private const string Windows =
        "{\"Ok\":{\"Windows\":[" +
        "{\"id\":7,\"title\":\"Report [draft]\",\"app_id\":\"editor\",\"workspace_id\":1,\"is_focused\":false,\"layout\":{\"window_size\":[800.0,600.0],\"tile_pos_in_workspace_view\":[10.0,20.0]}}," +
        "{\"id\":8,\"title\":\"Report final\",\"app_id\":\"viewer\",\"workspace_id\":2,\"is_focused\":true,\"is_floating\":true,\"layout\":{\"window_size\":[400.0,300.0],\"tile_pos_in_workspace_view\":[0.0,0.0]}}]}}";

    private const string Workspaces =
        "{\"Ok\":{\"Workspaces\":[" +
        "{\"id\":1,\"idx\":1,\"output\":\"DP-1\",\"is_active\":true,\"is_focused\":false}," +
        "{\"id\":2,\"idx\":1,\"output\":\"DP-2\",\"is_active\":true,\"is_focused\":true}]}}";

    private const string Outputs =
        "{\"Ok\":{\"Outputs\":{" +
        "\"DP-1\":{\"name\":\"DP-1\",\"logical\":{\"x\":0,\"y\":0,\"width\":1920,\"height\":1080}}," +
        "\"DP-2\":{\"name\":\"DP-2\",\"logical\":{\"x\":1920,\"y\":0,\"width\":1920,\"height\":1080}}}}}";

    private const string FocusedWindow =
        "{\"Ok\":{\"FocusedWindow\":{\"id\":8,\"title\":\"Report final\",\"app_id\":\"viewer\",\"workspace_id\":2,\"is_focused\":true,\"is_floating\":true,\"layout\":{\"window_size\":[400.0,300.0],\"tile_pos_in_workspace_view\":[0.0,0.0]}}}}";

    [Theory]
    [InlineData("plain")]
    [InlineData("work\"notes")]
    [InlineData("back\\slash")]
    [InlineData("quote\"} , \"Evil\": {\"x\": 1")]
    [InlineData("line\nbreak\tand \u0001 control")]
    [InlineData("ünïcödé 工作")]
    public async Task WorkspaceCommands_ShouldSerializeNamesAsEscapedJson(string name)
    {
        using var client = new FakeNiriIpcClient { Default = "{\"Ok\":{\"Handled\":null}}" };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.SwitchWorkspaceAsync(name, CancellationToken.None));
        Assert.True(await manager.MoveActiveWindowToWorkspaceAsync(name, CancellationToken.None));
        Assert.True(await manager.MoveWindowToWorkspaceByAddressAsync("5", name, CancellationToken.None));

        Assert.Equal(3, client.Requests.Count);
        foreach (var request in client.Requests)
        {
            using var document = JsonDocument.Parse(request);
            var action = document.RootElement.GetProperty("Action");
            var arguments = Assert.Single(action.EnumerateObject()).Value;
            Assert.Equal(name, arguments.GetProperty("reference").GetProperty("Name").GetString());
            Assert.Single(document.RootElement.EnumerateObject());
        }

        using var lastRequest = JsonDocument.Parse(client.Requests[2]);
        var lastArguments = Assert.Single(lastRequest.RootElement.GetProperty("Action").EnumerateObject()).Value;
        Assert.Equal(5UL, lastArguments.GetProperty("id").GetUInt64());
    }

    [Fact]
    public async Task WorkspaceCommands_ShouldAddressNumericWorkspacesById()
    {
        using var client = new FakeNiriIpcClient { Default = "{\"Ok\":{\"Handled\":null}}" };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.SwitchWorkspaceAsync("3", CancellationToken.None));
        Assert.True(await manager.MoveActiveWindowToWorkspaceAsync("3", CancellationToken.None));

        Assert.Equal(
            [
                "{\"Action\":{\"FocusWorkspace\":{\"reference\":{\"Id\":3}}}}",
                "{\"Action\":{\"MoveWindowToWorkspace\":{\"id\":null,\"reference\":{\"Id\":3},\"focus\":false}}}",
            ],
            client.Requests);
    }

    [Fact]
    public async Task WindowActions_ShouldBuildExpectedRequests()
    {
        using var client = new FakeNiriIpcClient { Default = "{\"Ok\":{\"Handled\":null}}" };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.FocusWindowByAddressAsync("12", CancellationToken.None));
        Assert.True(await manager.CloseWindowByAddressAsync("12", CancellationToken.None));
        Assert.True(await manager.FullscreenActiveWindowAsync(CancellationToken.None));
        Assert.True(await manager.MaximizeActiveWindowAsync(CancellationToken.None));
        Assert.True(await manager.ResizeActiveWindowAsync(800, 600, CancellationToken.None));

        Assert.Equal(
            [
                "{\"Action\":{\"FocusWindow\":{\"id\":12}}}",
                "{\"Action\":{\"CloseWindow\":{\"id\":12}}}",
                "{\"Action\":{\"FullscreenWindow\":{\"id\":null}}}",
                "{\"Action\":{\"MaximizeWindowToEdges\":{\"id\":null}}}",
                "{\"Action\":{\"SetWindowWidth\":{\"id\":null,\"change\":{\"SetFixed\":800}}}}",
                "{\"Action\":{\"SetWindowHeight\":{\"id\":null,\"change\":{\"SetFixed\":600}}}}",
            ],
            client.Requests);
    }

    [Theory]
    [InlineData("12abc")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("")]
    public async Task AddressCommands_ShouldRejectNonNumericAddresses(string address)
    {
        using var client = new FakeNiriIpcClient { Default = "{\"Ok\":{\"Handled\":null}}" };
        var manager = new NiriWindowManager(client);

        Assert.False(await manager.FocusWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await manager.CloseWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await manager.MoveWindowToWorkspaceByAddressAsync(address, "1", CancellationToken.None));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task ActionReply_ShouldBeSuccessfulOnlyWhenTheReplyIsOk()
    {
        using var client = new FakeNiriIpcClient { Default = "{\"Err\":\"no \\\"Ok\\\" window\"}" };
        var manager = new NiriWindowManager(client);

        Assert.False(await manager.FullscreenActiveWindowAsync(CancellationToken.None));

        client.Default = "not json";
        Assert.False(await manager.FullscreenActiveWindowAsync(CancellationToken.None));

        client.Default = null;
        Assert.False(await manager.FullscreenActiveWindowAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FocusWindowByTitleAsync_ShouldMatchLiteralTextAndFocusFirstWindow()
    {
        using var client = new FakeNiriIpcClient
        {
            Responses = { ["\"Windows\""] = Windows, ["\"Workspaces\""] = Workspaces, ["\"Outputs\""] = Outputs },
            Default = "{\"Ok\":{\"Handled\":null}}",
        };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.FocusWindowByTitleAsync("[draft]", CancellationToken.None));
        Assert.False(await manager.FocusWindowByTitleAsync("[.*]", CancellationToken.None));

        Assert.Equal("{\"Action\":{\"FocusWindow\":{\"id\":7}}}", Assert.Single(client.Requests, r => r.StartsWith("{\"Action", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GetWindowsAsync_ShouldPlaceTilesOnTheirOutput()
    {
        using var client = new FakeNiriIpcClient
        {
            Responses = { ["\"Windows\""] = Windows, ["\"Workspaces\""] = Workspaces, ["\"Outputs\""] = Outputs },
        };
        var manager = new NiriWindowManager(client);

        var windows = await manager.GetWindowsAsync(CancellationToken.None);

        Assert.Equal(2, windows.Count);
        Assert.Equal((10, 20, 800, 600), (windows[0].X, windows[0].Y, windows[0].Width, windows[0].Height));
        Assert.Equal((1920, 0), (windows[1].X, windows[1].Y));
        Assert.True(windows[1].IsFloating);
    }

    [Fact]
    public async Task MoveActiveWindowAsync_ShouldMoveToTargetOutputWorkspaceAndUseOutputRelativeCoordinates()
    {
        using var client = new FakeNiriIpcClient
        {
            Responses =
            {
                ["\"FocusedWindow\""] = FocusedWindow.Replace("\"workspace_id\":2", "\"workspace_id\":1", StringComparison.Ordinal),
                ["\"Workspaces\""] = Workspaces,
                ["\"Outputs\""] = Outputs,
            },
            Default = "{\"Ok\":{\"Handled\":null}}",
        };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.MoveActiveWindowAsync(2000, 100, CancellationToken.None));

        var actions = client.Requests.Where(r => r.StartsWith("{\"Action", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            [
                "{\"Action\":{\"MoveWindowToWorkspace\":{\"id\":null,\"reference\":{\"Id\":2},\"focus\":false}}}",
                "{\"Action\":{\"MoveFloatingWindow\":{\"id\":null,\"x\":{\"SetFixed\":80},\"y\":{\"SetFixed\":100}}}}",
            ],
            actions);
    }

    [Fact]
    public async Task CenterActiveWindowAsync_ShouldCenterOnTheWindowsOutput()
    {
        using var client = new FakeNiriIpcClient
        {
            Responses = { ["\"FocusedWindow\""] = FocusedWindow, ["\"Workspaces\""] = Workspaces, ["\"Outputs\""] = Outputs },
            Default = "{\"Ok\":{\"Handled\":null}}",
        };
        var manager = new NiriWindowManager(client);

        Assert.True(await manager.CenterActiveWindowAsync(CancellationToken.None));

        Assert.Equal(
            "{\"Action\":{\"MoveFloatingWindow\":{\"id\":null,\"x\":{\"SetFixed\":760},\"y\":{\"SetFixed\":390}}}}",
            client.Requests[^1]);
    }

    [Fact]
    public async Task Cancellation_ShouldPropagateInsteadOfBeingTurnedIntoAnEmptyResult()
    {
        using var client = new FakeNiriIpcClient();
        var manager = new NiriWindowManager(client);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetWindowsAsync(cts.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetActiveWindowAsync(cts.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.GetActiveWorkspaceAsync(cts.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.MoveActiveWindowAsync(1, 1, cts.Token));
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task MoveActiveWindowAsync_ShouldNotSendFinalMoveWhenCancelledWhileMappingOutputs()
    {
        using var cts = new CancellationTokenSource();
        using var client = new FakeNiriIpcClient
        {
            Responses = { ["\"Outputs\""] = Outputs },
            Default = "{\"Ok\":{\"Handled\":null}}",
            OnRequest = request =>
            {
                if (request is "\"FocusedWindow\"")
                {
                    cts.Cancel();
                }
            },
        };
        var manager = new NiriWindowManager(client);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.MoveActiveWindowAsync(100, 100, cts.Token));

        Assert.DoesNotContain(client.Requests, r => r.StartsWith("{\"Action", StringComparison.Ordinal));
    }

    private sealed class FakeNiriIpcClient : INiriIpcClient
    {
        public Dictionary<string, string?> Responses { get; } = new(StringComparer.Ordinal);

        public string? Default { get; set; }

        public Action<string>? OnRequest { get; init; }

        public List<string> Requests { get; } = [];

        public bool IsAvailable => true;

        public string? SocketPath => null;

        public Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(requestJson);
            OnRequest?.Invoke(requestJson);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Responses.TryGetValue(requestJson, out var response) ? response : Default);
        }

        public void Dispose() { }
    }
}
