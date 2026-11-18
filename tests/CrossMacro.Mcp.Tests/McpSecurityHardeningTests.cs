namespace CrossMacro.Mcp.Tests;

public sealed class McpSecurityHardeningTests
{
    [Fact]
    public void PathPolicy_ShouldRequireConfiguredCanonicalRoots()
    {
        var root = McpTestData.CreateTemporaryDirectory();
        var outside = McpTestData.CreateTemporaryDirectory();
        var file = Path.Combine(root, "safe.macro");
        File.WriteAllText(file, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [root]);
            var policy = new McpPathPolicy(new TestSettingsService(settings));

            Assert.True(policy.TryAuthorize(file, McpPathSetting.MacroRead, requireExisting: true, out var normalized, out var success));
            Assert.Equal(Path.GetFullPath(file), normalized);
            Assert.True(success.Success);

            var traversal = Path.Combine(root, "..", Path.GetFileName(outside), "outside.macro");
            Assert.False(policy.TryAuthorize(traversal, McpPathSetting.MacroRead, requireExisting: false, out _, out var denied));
            Assert.Equal("path_not_allowed", denied.Errors[0].Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void PathPolicy_ShouldRejectPersistedRelativeRootsAndAllowTheFilesystemRoot()
    {
        var temporaryRoot = McpTestData.GetPhysicalTemporaryRoot();
        var settings = new AppSettings();
        settings.McpSecurity.Paths = new McpPathSettings(
            macroReadRoots: ["relative-root"],
            macroWriteRoots: [],
            imageReadRoots: [],
            imageWriteRoots: [],
            fileReadRoots: [],
            fileWriteRoots: []);
        var policy = new McpPathPolicy(new TestSettingsService(settings));

        Assert.False(policy.TryAuthorize(temporaryRoot, McpPathSetting.MacroRead, requireExisting: true, out _, out var relativeRootFailure));
        Assert.Equal("path_not_allowed", relativeRootFailure.Errors[0].Code);

        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [Path.GetPathRoot(temporaryRoot)!]);

        Assert.True(policy.TryAuthorize(temporaryRoot, McpPathSetting.MacroRead, requireExisting: true, out _, out var rootSuccess));
        Assert.True(rootSuccess.Success);
    }

    [Fact]
    public void PathPolicy_ShouldRejectDanglingSymlinkPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = McpTestData.CreateTemporaryDirectory();
        var outside = McpTestData.CreateTemporaryDirectory();
        var link = Path.Combine(root, "dangling.macro");
        try
        {
            _ = File.CreateSymbolicLink(link, Path.Combine(outside, "target.macro"));
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroWrite, [root]);
            var policy = new McpPathPolicy(new TestSettingsService(settings));

            Assert.False(policy.TryAuthorize(link, McpPathSetting.MacroWrite, requireExisting: false, out _, out var failure));
            Assert.Equal("path_not_allowed", failure.Errors[0].Code);
        }
        finally
        {
            File.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

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

        await using var session = await GuardedSession.StartAsync<GuardedCommandTool>(guard);
        var result = await session.Client.CallToolAsync("command.execute", cancellationToken: session.Cancellation.Token);

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

        await using var session = await GuardedSession.StartAsync<GuardedAutomationTool>(guard);
        var run = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = " RUN " },
            cancellationToken: session.Cancellation.Token);
        var play = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "play" },
            cancellationToken: session.Cancellation.Token);
        var record = await session.Client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "record" },
            cancellationToken: session.Cancellation.Token);

        Assert.Equal(true, run.IsError);
        Assert.Equal(true, play.IsError);
        Assert.Equal(true, record.IsError);
        Assert.Equal(0, GuardedAutomationTool.InvocationCount);
    }

    private sealed class GuardedSession : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly Task _serverTask;

        private GuardedSession(ServiceProvider provider, Task serverTask, McpClient client, CancellationTokenSource cancellation)
        {
            _provider = provider;
            _serverTask = serverTask;
            Client = client;
            Cancellation = cancellation;
        }

        public McpClient Client { get; }

        public CancellationTokenSource Cancellation { get; }

        public static async Task<GuardedSession> StartAsync<TTool>(McpRequestGuard guard)
            where TTool : class
        {
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var services = new ServiceCollection();
            _ = services
                .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
                .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
                .WithRequestFilters(filters => filters.AddCallToolFilter(next =>
                    (context, token) => guard.InvokeAsync(
                        context.Params.Name,
                        () => next(context, token),
                        context.Params.Arguments)))
                .WithTools<TTool>();

            var provider = services.BuildServiceProvider(validateScopes: true);
            var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
            var client = await McpClient.CreateAsync(
                new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
                cancellationToken: cancellation.Token);
            return new GuardedSession(provider, serverTask, client, cancellation);
        }

        public async ValueTask DisposeAsync()
        {
            await Cancellation.CancelAsync();
            await _serverTask;
            await Client.DisposeAsync();
            await _provider.DisposeAsync();
            Cancellation.Dispose();
        }
    }

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
