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

            Assert.True(policy.TryAuthorize(file, McpPathKind.MacroRead, requireExisting: true, out var normalized, out var success));
            Assert.Equal(Path.GetFullPath(file), normalized);
            Assert.True(success.Success);

            var traversal = Path.Combine(root, "..", Path.GetFileName(outside), "outside.macro");
            Assert.False(policy.TryAuthorize(traversal, McpPathKind.MacroRead, requireExisting: false, out _, out var denied));
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

        Assert.False(policy.TryAuthorize(temporaryRoot, McpPathKind.MacroRead, requireExisting: true, out _, out var relativeRootFailure));
        Assert.Equal("path_not_allowed", relativeRootFailure.Errors[0].Code);

        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [Path.GetPathRoot(temporaryRoot)!]);

        Assert.True(policy.TryAuthorize(temporaryRoot, McpPathKind.MacroRead, requireExisting: true, out _, out var rootSuccess));
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

            Assert.False(policy.TryAuthorize(link, McpPathKind.MacroWrite, requireExisting: false, out _, out var failure));
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
    public void AuditStore_ShouldBeBounded()
    {
        var store = new McpAuditStore();
        for (var index = 0; index < McpAuditStore.MaximumEntries + 10; index++)
        {
            store.Record(new McpAuditEntry(
                DateTimeOffset.UtcNow,
                "command.execute",
                "Effectful",
                "denied",
                "denied"));
        }

        Assert.Equal(McpAuditStore.MaximumEntries, store.Snapshot().Count);
    }

    [Fact]
    public async Task RequestGuard_ShouldApproveEffectfulCallsAfterCapabilityChecks()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var audit = new McpAuditStore();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new AutoApprovalService(),
            audit,
            new TestSettingsService(settings),
            TimeProvider.System);
        var invoked = false;

        var result = await guard.InvokeAsync(
            "command.execute",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            },
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(invoked);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("approved", entry.Approval);
        Assert.Equal("success", entry.Result);
        Assert.Equal(["CommandExecute"], entry.Capabilities);
        Assert.Equal("mcp", entry.RuntimeIdentity);
        Assert.Equal("A permitted CrossMacro command.", entry.RedactedTarget);
    }

    [Fact]
    public async Task RequestGuard_ShouldBuildSanitizedApprovalIntentWithoutRequestPayloads()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var approval = new CapturingApprovalService();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            approval,
            new McpAuditStore(),
            new TestSettingsService(settings),
            TimeProvider.System);

        var result = await guard.InvokeAsync(
            "command.execute",
            () => ValueTask.FromResult(new CallToolResult { IsError = false }),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        var request = Assert.IsType<ApprovalRequest>(approval.Request);
        Assert.Equal("A permitted CrossMacro command.", request.TargetSummary);
        Assert.Equal(["CommandExecute"], request.CapabilityNames);
        Assert.DoesNotContain("secret", request.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestGuard_ShouldDenyUnregisteredToolsWithoutExecutingOrRetainingTheRequestedName()
    {
        var settings = new AppSettings();
        var audit = new McpAuditStore();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new AutoApprovalService(),
            audit,
            new TestSettingsService(settings),
            TimeProvider.System);
        var invoked = false;

        var result = await guard.InvokeAsync(
            "private.secret-value",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        Assert.False(invoked);
        Assert.Contains("not registered", Assert.Single(result.Content).ToString(), StringComparison.OrdinalIgnoreCase);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("unregistered", entry.ToolName);
        Assert.DoesNotContain("secret-value", entry.ToolName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestGuard_ShouldCancelAWaitingApprovalWhenTheMcpRequestIsCancelled()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var approval = new WaitingApprovalService();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            approval,
            new McpAuditStore(),
            new TestSettingsService(settings),
            TimeProvider.System);
        using var cancellation = new CancellationTokenSource();

        var invocation = guard.InvokeAsync(
            "command.execute",
            () => ValueTask.FromResult(new CallToolResult { IsError = false }),
            cancellation.Token).AsTask();
        await approval.Started.Task;

        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        await approval.Cancelled.Task;
    }

    [Fact]
    public async Task RequestGuard_ShouldMapApprovalServiceFailuresToStableOutcomeAndAudit()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var audit = new McpAuditStore();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new ThrowingApprovalService(),
            audit,
            new TestSettingsService(settings),
            TimeProvider.System);

        var invoked = false;
        var result = await guard.InvokeAsync(
            "command.execute",
            () =>
            {
                invoked = true;
                return ValueTask.FromResult(new CallToolResult { IsError = false });
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(invoked);
        Assert.Contains("approval", Assert.Single(result.Content).ToString(), StringComparison.OrdinalIgnoreCase);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("unavailable", entry.Approval);
        Assert.Equal("approval_unavailable", entry.Result);
        Assert.Equal(["CommandExecute"], entry.Capabilities);
    }

    [Fact]
    public async Task RequestGuard_ShouldKeepAuditTargetRedactedForEffectfulTools()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var audit = new McpAuditStore();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new AutoApprovalService(),
            audit,
            new TestSettingsService(settings),
            TimeProvider.System);

        _ = await guard.InvokeAsync(
            "command.execute",
            () => ValueTask.FromResult(new CallToolResult { IsError = false }),
            CancellationToken.None);

        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("mcp", entry.RuntimeIdentity);
        Assert.Equal("A permitted CrossMacro command.", entry.RedactedTarget);
        Assert.DoesNotContain("payload", entry.RedactedTarget, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", entry.RedactedTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestFilter_ShouldApproveAnEffectfulCallAfterCapabilityChecks()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new AutoApprovalService(),
            new McpAuditStore(),
            new TestSettingsService(settings),
            TimeProvider.System);
        GuardedCommandTool.Invoked = false;
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var services = new ServiceCollection();
        _ = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithRequestFilters(filters => filters.AddCallToolFilter(next =>
                (context, token) => guard.InvokeAsync(
                    context.Params.Name,
                    () => next(context, token),
                    token,
                    context.Params.Arguments)))
            .WithTools<GuardedCommandTool>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: cancellation.Token);

        var result = await client.CallToolAsync("command.execute", cancellationToken: cancellation.Token);

        Assert.NotEqual(true, result.IsError);
        Assert.True(GuardedCommandTool.Invoked);
        await cancellation.CancelAsync();
        await serverTask;
    }

    [Fact]
    public async Task RequestFilter_ShouldUseOperationSpecificAutomationCapabilities()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        settings.McpSecurity.AllowRecording = false;
        settings.McpSecurity.AllowCommandExecute = true;
        var audit = new McpAuditStore();
        var guard = new McpRequestGuard(
            new McpCapabilityPolicy(new TestSettingsService(settings)),
            new AutoApprovalService(),
            audit,
            new TestSettingsService(settings),
            TimeProvider.System);
        GuardedAutomationTool.InvocationCount = 0;
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var services = new ServiceCollection();
        _ = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithRequestFilters(filters => filters.AddCallToolFilter(next =>
                (context, token) => guard.InvokeAsync(
                    context.Params.Name,
                    () => next(context, token),
                    token,
                    context.Params.Arguments)))
            .WithTools<GuardedAutomationTool>();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: cancellation.Token);

        var run = await client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?> { ["kind"] = " RUN " },
            cancellationToken: cancellation.Token);
        var play = await client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?> { ["kind"] = "play" },
            cancellationToken: cancellation.Token);
        var record = await client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?> { ["kind"] = "record" },
            cancellationToken: cancellation.Token);

        Assert.NotEqual(true, run.IsError);
        Assert.Equal(true, play.IsError);
        Assert.Equal(true, record.IsError);
        Assert.Equal(1, GuardedAutomationTool.InvocationCount);
        Assert.Collection(
            audit.Snapshot(),
            entry =>
            {
                Assert.Equal("success", entry.Result);
                Assert.Equal(["CommandExecute"], entry.Capabilities);
            },
            entry =>
            {
                Assert.Equal("denied", entry.Result);
                Assert.Equal(["MacroRead", "InputAutomation"], entry.Capabilities);
            },
            entry =>
            {
                Assert.Equal("denied", entry.Result);
                Assert.Equal(["Recording", "FileWrite"], entry.Capabilities);
            });
        await cancellation.CancelAsync();
        await serverTask;
    }

    private sealed class TestSettingsService(AppSettings settings) : ISettingsService
    {
        public AppSettings Current { get; } = settings;

        public AppSettings Load() => Current;

        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

        public void Save()
        {
        }

        public Task SaveAsync() => Task.CompletedTask;
    }

    private sealed class WaitingApprovalService : IApprovalService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ApprovalResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                return ApprovalResult.Approved;
            }
            catch (OperationCanceledException)
            {
                Cancelled.SetResult();
                throw;
            }
        }
    }

    private sealed class ThrowingApprovalService : IApprovalService
    {
        public Task<ApprovalResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("approval transport unavailable");
    }

    private sealed class CapturingApprovalService : IApprovalService
    {
        public ApprovalRequest? Request { get; private set; }

        public Task<ApprovalResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(ApprovalResult.Denied);
        }
    }

    private sealed class GuardedCommandTool
    {
        public static bool Invoked { get; set; }

        [McpServerTool(Name = "command.execute", ReadOnly = false, Destructive = true, Idempotent = false)]
        public string Execute()
        {
            Invoked = true;
            return "This tool should not run.";
        }
    }

    private sealed class GuardedAutomationTool
    {
        public static int InvocationCount { get; set; }

        [McpServerTool(Name = "automation.start", ReadOnly = false, Destructive = false, Idempotent = false)]
        public string Start(string kind)
        {
            InvocationCount++;
            return kind;
        }
    }
}
