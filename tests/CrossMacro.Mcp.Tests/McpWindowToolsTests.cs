namespace CrossMacro.Mcp.Tests;

public sealed class McpWindowToolsTests
{
    [Fact]
    public async Task QueryWindowsAsync_ShouldReturnBoundedListAndPreserveTheTotalCount()
    {
        var windows = Enumerable.Range(0, 101)
            .Select(index => McpTestData.CreateWindow(index))
            .ToArray();
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Windows listed.", new WindowListData(windows, windows.Length)),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "list",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("list", structured.GetProperty("mode").GetString());
        Assert.Equal(100, structured.GetProperty("windows").GetArrayLength());
        Assert.Equal(101, structured.GetProperty("totalCount").GetInt32());
        Assert.True(structured.GetProperty("isTruncated").GetBoolean());
        Assert.Equal(WindowCliAction.List, windowService.LastOptions?.Action);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldMapActiveWindowThroughTheCliService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Active window read.", McpTestData.CreateWindow(1)),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "active",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("active", structured.GetProperty("mode").GetString());
        Assert.Equal("0x1", structured.GetProperty("windows")[0].GetProperty("address").GetString());
        Assert.Equal(1, structured.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldUseTitleSelectorAndBoundedWaitTimeout()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Window wait matched.",
                new WindowWaitData(
                    Found: true,
                    Window: McpTestData.CreateWindow(4),
                    TimeoutMs: 30_000)),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "wait",
            selectorKind: "title",
            selectorValue: "Editor",
            timeoutMs: 30_000,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(WindowCliAction.Wait, windowService.LastOptions?.Action);
        Assert.Equal(WindowSelectorKind.Title, windowService.LastOptions?.Selector?.Kind);
        Assert.Equal("Editor", windowService.LastOptions?.Selector?.Value);
        Assert.Equal(30_000, windowService.LastOptions?.TimeoutMs);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.True(structured.GetProperty("found").GetBoolean());
        Assert.Equal(30_000, structured.GetProperty("timeoutMs").GetInt32());
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldUseClassSelectorForSearchAndDefaultWaitTimeout()
    {
        var searchService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Window search complete.",
                new WindowListData([McpTestData.CreateWindow(2)], Count: 1)),
        };
        var searchTools = McpToolTestFactory.CreateWindowTools(windowCliService: searchService);

        var search = await searchTools.QueryWindowsAsync(
            mode: "search",
            selectorKind: "class",
            selectorValue: "TestApp",
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, search.IsError);
        Assert.Equal(WindowCliAction.Search, searchService.LastOptions?.Action);
        Assert.Equal(WindowSelectorKind.Class, searchService.LastOptions?.Selector?.Kind);
        Assert.Null(searchService.LastOptions?.TimeoutMs);

        var waitService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window wait timed out.", new WindowWaitData(Found: false, Window: null, TimeoutMs: 5_000)),
        };
        var waitTools = McpToolTestFactory.CreateWindowTools(windowCliService: waitService);

        var wait = await waitTools.QueryWindowsAsync(
            mode: "wait",
            selectorKind: "class",
            selectorValue: "TestApp",
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, wait.IsError);
        Assert.Equal(5_000, waitService.LastOptions?.TimeoutMs);
        var structured = Assert.IsType<JsonElement>(wait.StructuredContent);
        Assert.False(structured.GetProperty("found").GetBoolean());
        Assert.Equal(5_000, structured.GetProperty("timeoutMs").GetInt32());
    }

    [Theory]
    [InlineData("unknown", null, null, null)]
    [InlineData("active", "title", "Editor", null)]
    [InlineData("search", null, null, null)]
    [InlineData("search", "address", "0x1", null)]
    [InlineData("wait", "class", "Code", 30_001)]
    public async Task QueryWindowsAsync_ShouldRejectUnsupportedInputsWithoutInvokingTheCliService(
        string mode,
        string? selectorKind,
        string? selectorValue,
        int? timeoutMs)
    {
        var windowService = new TestWindowCliService();
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: mode,
            selectorKind: selectorKind,
            selectorValue: selectorValue,
            timeoutMs: timeoutMs,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, windowService.CallCount);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldRedactUnsupportedBackendDetails()
    {
        const string secret = "window backend detail should not leak";
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Window management is not supported in this runtime.",
                [secret]),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "list",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldPropagateCancellation()
    {
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: new TestWindowCliService());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tools.QueryWindowsAsync(
                mode: "list",
                selectorKind: null,
                selectorValue: null,
                timeoutMs: null,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ControlWindowsAsync_ShouldMapMutationsAndSelectorsToTheExistingWindowService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window move complete.", new WindowMutationData("move", Result: true)),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var moved = await tools.ControlWindowsAsync(
            action: "move",
            x: 120,
            y: 240,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, moved.IsError);
        Assert.Equal(WindowCliAction.Move, windowService.LastOptions!.Action);
        Assert.Equal(120, windowService.LastOptions.X);
        Assert.Equal(240, windowService.LastOptions.Y);
        var movedStructured = Assert.IsType<JsonElement>(moved.StructuredContent);
        Assert.True(movedStructured.GetProperty("changed").GetBoolean());
        Assert.Equal("move", movedStructured.GetProperty("action").GetString());

        var focused = await tools.ControlWindowsAsync(
            action: "focus",
            selectorKind: "class",
            selectorValue: "Editor",
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, focused.IsError);
        Assert.Equal(WindowCliAction.Focus, windowService.LastOptions.Action);
        Assert.Equal(WindowSelectorKind.Class, windowService.LastOptions.Selector!.Kind);
        Assert.Equal("Editor", windowService.LastOptions.Selector.Value);
    }

    [Fact]
    public async Task ControlWindowsAsync_ShouldValidateDangerousSelectorsAndGeometryBeforeCallingTheService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window control complete.", new WindowMutationData("close", Result: true)),
        };
        var tools = McpToolTestFactory.CreateWindowTools(windowCliService: windowService);

        var invalidClose = await tools.ControlWindowsAsync(
            action: "close",
            selectorKind: "class",
            selectorValue: "Editor",
            cancellationToken: CancellationToken.None);
        var invalidResize = await tools.ControlWindowsAsync(
            action: "resize",
            x: 0,
            y: 100,
            cancellationToken: CancellationToken.None);
        var invalidWorkspace = await tools.ControlWindowsAsync(
            action: "workspace_move_window",
            selectorKind: "title",
            selectorValue: "Editor",
            workspaceName: "2",
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, invalidClose.IsError);
        Assert.Equal(true, invalidResize.IsError);
        Assert.Equal(true, invalidWorkspace.IsError);
        Assert.Equal(0, windowService.CallCount);
    }
}
