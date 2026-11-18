namespace CrossMacro.Mcp.Tests;

public sealed class McpSecurityHardeningTests
{
    [Fact]
    public async Task RequestGuard_ShouldRunEffectfulCallsWhoseCapabilitiesAreEnabled()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var guard = new McpRequestGuard(new McpCapabilityPolicy(new TestSettingsService(settings)));
        var invoked = false;

        var result = await guard.InvokeAsync(
            "command.execute",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            });

        Assert.False(result.IsError);
        Assert.True(invoked);
    }

    [Fact]
    public async Task RequestGuard_ShouldDenyEffectfulCallsWhoseCapabilitiesAreDisabled()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = false;
        var guard = new McpRequestGuard(new McpCapabilityPolicy(new TestSettingsService(settings)));
        var invoked = false;

        var result = await guard.InvokeAsync(
            "command.execute",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            });

        Assert.True(result.IsError);
        Assert.False(invoked);
    }

    [Fact]
    public async Task RequestGuard_ShouldDenyUnregisteredToolsWithoutExecutingThem()
    {
        var guard = new McpRequestGuard(new McpCapabilityPolicy(new TestSettingsService(new AppSettings())));
        var invoked = false;

        var result = await guard.InvokeAsync(
            "private.secret-value",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            });

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        Assert.False(invoked);
        var message = Assert.Single(result.Content).ToString();
        Assert.Contains("not registered", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestFilter_ShouldRunAnEffectfulCallWhoseCapabilitiesAreEnabled()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var guard = new McpRequestGuard(new McpCapabilityPolicy(new TestSettingsService(settings)));
        GuardedCommandTool.Invoked = false;

        await using var session = await StartGuardedAsync<GuardedCommandTool>(guard);
        var result = await session.Client.CallToolAsync("command.execute", cancellationToken: session.Token);

        Assert.NotEqual(true, result.IsError);
        Assert.True(GuardedCommandTool.Invoked);
    }

    [Fact]
    public async Task RequestFilter_ShouldUseOperationSpecificAutomationCapabilities()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        settings.McpSecurity.AllowRecording = false;
        settings.McpSecurity.AllowCommandExecute = true;
        var guard = new McpRequestGuard(new McpCapabilityPolicy(new TestSettingsService(settings)));
        GuardedAutomationTool.InvocationCount = 0;

        await using var session = await StartGuardedAsync<GuardedAutomationTool>(guard);
        var run = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = " RUN " },
            cancellationToken: session.Token);
        var play = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "play" },
            cancellationToken: session.Token);
        var record = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "record" },
            cancellationToken: session.Token);

        Assert.Equal(true, run.IsError);
        Assert.Equal(true, play.IsError);
        Assert.Equal(true, record.IsError);
        Assert.Equal(0, GuardedAutomationTool.InvocationCount);
    }

    private static Task<InProcessMcpServer> StartGuardedAsync<TTool>(McpRequestGuard guard)
        where TTool : class =>
        InProcessMcpServer.StartAsync(builder => builder
            .WithRequestFilters(filters => filters.AddCallToolFilter(next =>
                (context, token) => guard.InvokeAsync(
                    context.Params.Name,
                    () => next(context, token),
                    context.Params.Arguments)))
            .WithTools<TTool>());

    private sealed class GuardedCommandTool
    {
        private GuardedCommandTool() { }

        public static bool Invoked { get; set; }

        [McpServerTool(Name = "command.execute", ReadOnly = false, Destructive = true, Idempotent = false)]
        public static string Execute()
        {
            Invoked = true;
            return "This tool should not run.";
        }
    }

    private sealed class GuardedAutomationTool
    {
        private GuardedAutomationTool() { }

        public static int InvocationCount { get; set; }

        [McpServerTool(Name = "automation.start", ReadOnly = false, Destructive = false, Idempotent = false)]
        public static string Start(string kind)
        {
            InvocationCount++;
            return kind;
        }
    }
}
