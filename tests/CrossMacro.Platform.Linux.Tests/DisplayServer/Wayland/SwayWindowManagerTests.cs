using CrossMacro.Platform.Abstractions.WindowManagement;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class SwayWindowManagerTests
{
    private const string TreeWithTwoWindows =
        "{\"id\":1,\"type\":\"root\",\"nodes\":[{\"id\":2,\"type\":\"workspace\",\"name\":\"main\",\"nodes\":[" +
        "{\"id\":3,\"type\":\"con\",\"name\":\"Report [draft] (1).txt\",\"app_id\":\"org.example.Editor\"}," +
        "{\"id\":4,\"type\":\"con\",\"name\":\"Report [final]\",\"app_id\":\"org.example.Viewer\",\"focused\":true}]}]}";

    [Fact]
    public async Task GetActiveWindowAsync_ShouldMapFocusedNestedNodeWithItsWorkspace()
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
        Assert.Equal("main", result.Workspace);
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
    public async Task ActiveWorkspace_ShouldReadFocusedWorkspaceName()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses = { [1] = "[{\"name\":\"other\"},{\"name\":\"main\",\"focused\":true}]" },
        };
        var manager = new SwayWindowManager(client);

        Assert.Equal("main", await manager.GetActiveWorkspaceAsync(CancellationToken.None));
        Assert.Equal((uint)1, Assert.Single(client.Requests).Type);
    }

    [Fact]
    public async Task FocusWindowByTitleAsync_ShouldMatchLiteralTextAndAddressFirstWindowByContainerId()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses = { [0] = "[{\"success\":true}]", [4] = TreeWithTwoWindows },
        };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.FocusWindowByTitleAsync("[draft] (1)", CancellationToken.None));

        Assert.Equal("[con_id=3] focus", Assert.Single(client.Requests, r => r.Type is 0).Payload);
    }

    [Theory]
    [InlineData("[.*]")]
    [InlineData("Report .*")]
    [InlineData("nothing")]
    public async Task FocusWindowByTitleAsync_ShouldNotTreatTextAsRegex(string needle)
    {
        using var client = new FakeSwayIpcClient
        {
            Responses = { [0] = "[{\"success\":true}]", [4] = TreeWithTwoWindows },
        };
        var manager = new SwayWindowManager(client);

        Assert.False(await manager.FocusWindowByTitleAsync(needle, CancellationToken.None));

        Assert.DoesNotContain(client.Requests, r => r.Type is 0);
    }

    [Fact]
    public async Task FocusWindowByClassAsync_ShouldFocusSingleWindowByContainerId()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses = { [0] = "[{\"success\":true}]", [4] = TreeWithTwoWindows },
        };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.FocusWindowByClassAsync("viewer", CancellationToken.None));

        Assert.Equal("[con_id=4] focus", Assert.Single(client.Requests, r => r.Type is 0).Payload);
    }

    [Fact]
    public async Task CloseWindowByTitleAsync_ShouldKillOnlyTheFirstMatchingContainer()
    {
        using var client = new FakeSwayIpcClient
        {
            Responses = { [0] = "[{\"success\":true}]", [4] = TreeWithTwoWindows },
        };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.CloseWindowByTitleAsync("Report", CancellationToken.None));

        Assert.Equal("[con_id=3] kill", Assert.Single(client.Requests, r => r.Type is 0).Payload);
    }

    [Theory]
    [InlineData("3] kill; [con_id=4")]
    [InlineData("3; kill")]
    [InlineData("-3")]
    [InlineData("0x3")]
    [InlineData("")]
    public async Task AddressCommands_ShouldRejectAddressesThatAreNotContainerIds(string address)
    {
        using var client = new FakeSwayIpcClient { Responses = { [0] = "[{\"success\":true}]" } };
        var manager = new SwayWindowManager(client);

        Assert.False(await manager.FocusWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await manager.CloseWindowByAddressAsync(address, CancellationToken.None));
        Assert.False(await manager.MoveWindowToWorkspaceByAddressAsync(address, "1", CancellationToken.None));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task AddressCommands_ShouldAddressContainerByNumericId()
    {
        using var client = new FakeSwayIpcClient { Responses = { [0] = "[{\"success\":true}]" } };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.FocusWindowByAddressAsync("17", CancellationToken.None));
        Assert.True(await manager.CloseWindowByAddressAsync("17", CancellationToken.None));
        Assert.True(await manager.MoveWindowToWorkspaceByAddressAsync("17", "web", CancellationToken.None));

        Assert.Equal(
            ["[con_id=17] focus", "[con_id=17] kill", "[con_id=17] move container to workspace \"web\""],
            client.Requests.Select(r => r.Payload).ToArray());
    }

    [Theory]
    [InlineData("1; kill")]
    [InlineData("1, exec touch /tmp/pwned")]
    [InlineData("a\"; exec x; \"b")]
    [InlineData("it's; kill")]
    public async Task WorkspaceCommands_ShouldKeepWorkspaceNameInsideOneArgument(string workspace)
    {
        using var client = new FakeSwayIpcClient { Responses = { [0] = "[{\"success\":true}]" } };
        var manager = new SwayWindowManager(client);

        Assert.True(await manager.SwitchWorkspaceAsync(workspace, CancellationToken.None));
        Assert.True(await manager.MoveActiveWindowToWorkspaceAsync(workspace, CancellationToken.None));
        Assert.True(await manager.MoveWindowToWorkspaceByAddressAsync("9", workspace, CancellationToken.None));

        int[] expectedWordCounts = [2, 5, 5];
        Assert.Equal(expectedWordCounts.Length, client.Requests.Count);
        for (var i = 0; i < expectedWordCounts.Length; i++)
        {
            var command = Assert.Single(SwayIpcCommandParser.Parse(client.Requests[i].Payload));
            Assert.Equal(expectedWordCounts[i], command.Words.Count);
            Assert.Equal(workspace, command.Words[^1]);
        }
    }

    [Fact]
    public async Task WorkspaceCommands_ShouldRejectNamesThatCannotBeQuotedSafely()
    {
        using var client = new FakeSwayIpcClient { Responses = { [0] = "[{\"success\":true}]" } };
        var manager = new SwayWindowManager(client);

        Assert.False(await manager.SwitchWorkspaceAsync("back\\slash", CancellationToken.None));
        Assert.False(await manager.SwitchWorkspaceAsync("line\nbreak", CancellationToken.None));
        Assert.False(await manager.SwitchWorkspaceAsync(" ", CancellationToken.None));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Commands_ShouldReportFailureWhenAnyChainedCommandFails()
    {
        using var client = new FakeSwayIpcClient { Responses = { [0] = "[{\"success\":true},{\"success\":false,\"error\":\"No matching node.\"}]" } };
        var manager = new SwayWindowManager(client);

        Assert.False(await manager.FullscreenActiveWindowAsync(CancellationToken.None));
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
        Assert.False(await manager.FullscreenActiveWindowAsync(CancellationToken.None));
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
