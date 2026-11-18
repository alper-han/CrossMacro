namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdeWindowManagerTests
{
    [Fact]
    public void BuildOneShotScriptContent_ShouldRouteCallbackToConnectionSpecificDestination()
    {
        const string template = "callDBus('__SERVICE_NAME__', '__OBJECT_PATH__', '__INTERFACE__', 'ReportWindowData', '__CORRELATION_ID__');";

        var first = KdeWindowManager.BuildOneShotScriptContent(template, "first", ":1.42");
        var second = KdeWindowManager.BuildOneShotScriptContent(template, "second", ":1.43");

        Assert.Contains("callDBus(':1.42'", first, StringComparison.Ordinal);
        Assert.Contains("'first'", first, StringComparison.Ordinal);
        Assert.DoesNotContain(":1.43", first, StringComparison.Ordinal);
        Assert.Contains("callDBus(':1.43'", second, StringComparison.Ordinal);
        Assert.Contains("'second'", second, StringComparison.Ordinal);
        Assert.DoesNotContain(":1.42", second, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOperationScript_AppendsExactlyOneCallWithTheGivenArguments()
    {
        var script = KdeWindowManager.BuildOperationScript("moveToWorkspaceByAddress", "'0xabc'", "'Desk 2'");

        Assert.Contains("function crossmacroRun(operation, args)", script, StringComparison.Ordinal);
        Assert.EndsWith("\ncrossmacroRun('moveToWorkspaceByAddress', ['0xabc', 'Desk 2']);\n", script, StringComparison.Ordinal);
        Assert.Equal(1, script.Split("\ncrossmacroRun('", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("activeWindow")]
    [InlineData("windows")]
    [InlineData("activeWorkspace")]
    [InlineData("focusByAddress")]
    [InlineData("focusByTitle")]
    [InlineData("focusByClass")]
    [InlineData("closeByAddress")]
    [InlineData("closeByTitle")]
    [InlineData("moveActive")]
    [InlineData("resizeActive")]
    [InlineData("fullscreenActive")]
    [InlineData("maximizeActive")]
    [InlineData("centerActive")]
    [InlineData("switchWorkspace")]
    [InlineData("moveActiveToWorkspace")]
    [InlineData("moveToWorkspaceByAddress")]
    public void BuildOperationScript_OnlyCallsOperationsTheEmbeddedScriptDefines(string operation)
    {
        var script = KdeWindowManager.BuildOperationScript(operation);

        Assert.Contains($"\n    {operation}: function (", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOperationScript_KeepsTheCallbackPlaceholdersForTheOneShotSubstitution()
    {
        var script = KdeWindowManager.BuildOperationScript("windows");

        var content = KdeWindowManager.BuildOneShotScriptContent(script, "corr", ":1.7");

        Assert.Contains("callDBus(':1.7', '/io/github/alper_han/crossmacro/WindowManager'", content, StringComparison.Ordinal);
        Assert.Contains("'corr'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("__CORRELATION_ID__", content, StringComparison.Ordinal);
        Assert.DoesNotContain("__SERVICE_NAME__", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ok", false)]
    public void IsSuccessfulMutationReply_RequiresTheScriptToReportTrue(string? reply, bool expected)
    {
        Assert.Equal(expected, KdeWindowManager.IsSuccessfulMutationReply(reply));
    }

    [Fact]
    public void CreateOneShotPluginName_ShouldPreserveCorrelationIdentity()
    {
        var first = KdeWindowManager.CreateOneShotPluginName("first");
        var second = KdeWindowManager.CreateOneShotPluginName("second");

        Assert.Equal("crossmacro-window-first", first);
        Assert.Equal("crossmacro-window-second", second);
        Assert.False(string.Equals(first, second, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AwaitCallbackAsync_ShouldReturnOriginalCallbackPayload()
    {
        const string expected = "{\"name\":\"Desktop 1\"}";

        var result = await KdeWindowManager.AwaitCallbackAsync(
            Task.FromResult(expected),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task AwaitCallbackAsync_WhenDeadlineExpires_ShouldReturnNull()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await KdeWindowManager.AwaitCallbackAsync(
            pending.Task,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AwaitCallbackAsync_WhenCallerCancels_ShouldPropagateCancellation()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KdeWindowManager.AwaitCallbackAsync(
                pending.Task,
                TimeSpan.FromSeconds(1),
                cancellation.Token));
    }

    [Fact]
    public async Task DisposeAsync_MarksManagerDisposedAndRejectsLaterOperations()
    {
        var manager = new KdeWindowManager();

        await manager.DisposeAsync();

        Assert.True(manager.IsDisposed);
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.GetActiveWindowAsync(TestContext.Current.CancellationToken));
    }
}
