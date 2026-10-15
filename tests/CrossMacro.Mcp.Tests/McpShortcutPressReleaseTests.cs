namespace CrossMacro.Mcp.Tests;

public sealed class McpShortcutPressReleaseTests
{
    private static readonly string[] ProtocolHotkeys = ["F7"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_RedactsReleaseExactlyLikePress(bool macroRead)
    {
        using var fixture = new Fixture(macroRead);
        await fixture.Manage.AddAsync(new ShortcutTask { MacroFilePath = fixture.Press, ReleaseMacroFilePath = fixture.Release, RunOnPressAndRelease = true, Hotkeys = { "F7" } }, TestContext.Current.CancellationToken);
        var result = await fixture.Tools.ListShortcutsAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.SerializeToElement(Assert.Single(result.Tasks), JsonOptions);
        Assert.True(json.GetProperty("runOnPressAndRelease").GetBoolean());
        Assert.Equal(macroRead ? fixture.Press : string.Empty, json.GetProperty("macroFilePath").GetString());
        Assert.Equal(macroRead ? fixture.Release : string.Empty, json.GetProperty("releaseMacroFilePath").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_RejectsDeniedReleaseIncludingReplacementAfterPreflight(bool replaceAfterPreflight)
    {
        using var fixture = new Fixture();
        var initial = new ShortcutTask { MacroFilePath = fixture.Press, ReleaseMacroFilePath = replaceAfterPreflight ? fixture.Release : fixture.Denied, RunOnPressAndRelease = true, Hotkeys = { "F7" } };
        await fixture.Manage.AddAsync(initial, TestContext.Current.CancellationToken);
        fixture.Commands.AfterList = replaceAfterPreflight ? () => fixture.Store.Tasks[0].ReleaseMacroFilePath = fixture.Denied : null;
        var result = await fixture.Tools.RunShortcutAsync(initial.Id.ToString(), TestContext.Current.CancellationToken);
        Assert.False(result.Outcome.Success);
        Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        Assert.Equal(0, fixture.Store.RunCount);
    }

    [Fact]
    public async Task ProtocolSmoke_AddAndEditPair_UsesAdvertisedFieldsAndPersistsConfiguration()
    {
        using var fixture = new Fixture();
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var services = new ServiceCollection();
        services.AddSingleton(fixture.Tools);
        services.AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithTools<McpTaskTools>(fixture.Tools, McpJsonContext.Default.Options);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
        await using var client = await McpClient.CreateAsync(new StreamClientTransport(
            clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()), cancellationToken: cancellation.Token);
        var tool = (await client.ListToolsAsync(cancellationToken: cancellation.Token)).Single(tool => tool.Name is "shortcut.add");
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("runOnPressAndRelease", out _));
        Assert.True(properties.TryGetProperty("releaseMacroFilePath", out _));
        var added = await client.CallToolAsync("shortcut.add", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "Protocol Pair",
            ["macroPath"] = fixture.Press,
            ["hotkeys"] = ProtocolHotkeys,
            ["runOnPressAndRelease"] = true,
            ["releaseMacroFilePath"] = fixture.Release,
        }, cancellationToken: cancellation.Token);
        Assert.False(added.IsError is true, JsonSerializer.Serialize(added));
        var returnedTask = Assert.IsType<JsonElement>(added.StructuredContent).GetProperty("task");
        Assert.True(returnedTask.GetProperty("runOnPressAndRelease").GetBoolean());
        Assert.Equal(fixture.Release, returnedTask.GetProperty("releaseMacroFilePath").GetString());
        var stored = Assert.Single(fixture.Store.Tasks);
        Assert.True(stored.RunOnPressAndRelease);
        Assert.Equal(fixture.Release, stored.ReleaseMacroFilePath);
        var edited = await client.CallToolAsync("shortcut.edit", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["taskId"] = stored.Id.ToString(),
            ["runOnPressAndRelease"] = false,
        }, cancellationToken: cancellation.Token);
        Assert.False(edited.IsError is true, JsonSerializer.Serialize(edited));
        Assert.False(Assert.Single(fixture.Store.Tasks).RunOnPressAndRelease);
        Assert.Empty(Assert.Single(fixture.Store.Tasks).ReleaseMacroFilePath);
        await cancellation.CancelAsync();
        await serverTask;
    }

    [Fact]
    public async Task DedicatedTools_AddEditAndExitMode_PersistTheActualShortcutConfiguration()
    {
        using var fixture = new Fixture();
        var added = await fixture.Tools.AddShortcutAsync("Pair", fixture.Press, ["F7"],
            runOnPressAndRelease: true, releaseMacroFilePath: fixture.Release, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(added.Outcome.Success);
        Assert.True(Assert.Single(fixture.Store.Tasks).RunOnPressAndRelease);
        Assert.Equal(fixture.Release, Assert.Single(fixture.Store.Tasks).ReleaseMacroFilePath);
        var id = added.Task!.Id.ToString();
        var edited = await fixture.Tools.EditShortcutAsync(id, releaseMacroFilePath: fixture.Press, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(edited.Outcome.Success);
        Assert.Equal(fixture.Press, Assert.Single(fixture.Store.Tasks).ReleaseMacroFilePath);
        var normal = await fixture.Tools.EditShortcutAsync(id, runOnPressAndRelease: false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(normal.Outcome.Success);
        Assert.False(Assert.Single(fixture.Store.Tasks).RunOnPressAndRelease);
        Assert.Empty(Assert.Single(fixture.Store.Tasks).ReleaseMacroFilePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Add_RejectsDeniedReleaseBeforePersistenceForDedicatedAndCommandTools(bool commandBridge)
    {
        using var fixture = new Fixture();
        if (commandBridge)
        {
            var handler = new CrossMacro.Cli.Commands.ShortcutCommandHandler(fixture.Commands);
            var tools = new McpCommandTools(McpToolTestFactory.CreateMacroExecutionService(), McpToolTestFactory.CreateOperationCoordinator(),
                McpToolTestFactory.CreateRunScriptExecutionService(), McpToolTestFactory.CreateRecordExecutionService(),
                McpToolTestFactory.CreatePreflightService(), new CliCommandExecutor(new TestCliCommandHandlerResolver(handler)),
                new TestProfileOperations(), new McpCommandPolicy(), fixture.Authorization, fixture.PathAuthorizer);
            var result = await tools.ExecuteCommandAsync("shortcut", ["add", "--name", "Pair", "--macro", fixture.Press, "--hotkey", "F7", "--press-release", "--release-macro", fixture.Denied], TestContext.Current.CancellationToken);
            Assert.True(result.IsError);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        else
        {
            var result = await fixture.Tools.AddShortcutAsync("Pair", fixture.Press, ["F7"],
                runOnPressAndRelease: true, releaseMacroFilePath: fixture.Denied, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        }
        Assert.Equal(0, fixture.Store.CommitCount);
        Assert.Empty(fixture.Store.Tasks);
    }

    [Fact]
    public async Task Edit_RejectsExplicitBlankReleaseInsteadOfSilentlyReusingStoredPath()
    {
        using var fixture = new Fixture();
        var task = await fixture.Manage.AddAsync(new ShortcutTask
        {
            MacroFilePath = fixture.Press,
            ReleaseMacroFilePath = fixture.Release,
            RunOnPressAndRelease = true,
            Hotkeys = { "F7" },
        }, TestContext.Current.CancellationToken);
        var commits = fixture.Store.CommitCount;
        var result = await fixture.Tools.EditShortcutAsync(task.Id.ToString(), releaseMacroFilePath: " ", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Outcome.Success);
        Assert.Equal(commits, fixture.Store.CommitCount);
        Assert.Equal(fixture.Release, Assert.Single(fixture.Store.Tasks).ReleaseMacroFilePath);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EditAndEnable_AuthorizesReplacementInsteadOfDiscardedRelease(bool commandBridge, bool exitPairMode)
    {
        using var fixture = new Fixture();
        var task = await fixture.Manage.AddAsync(new ShortcutTask
        {
            MacroFilePath = fixture.Press,
            ReleaseMacroFilePath = fixture.Denied,
            RunOnPressAndRelease = true,
            IsEnabled = false,
            Hotkeys = { "F7" },
        }, TestContext.Current.CancellationToken);
        if (commandBridge)
        {
            var handler = new CrossMacro.Cli.Commands.ShortcutCommandHandler(fixture.Commands);
            var tools = new McpCommandTools(McpToolTestFactory.CreateMacroExecutionService(), McpToolTestFactory.CreateOperationCoordinator(),
                McpToolTestFactory.CreateRunScriptExecutionService(), McpToolTestFactory.CreateRecordExecutionService(),
                McpToolTestFactory.CreatePreflightService(), new CliCommandExecutor(new TestCliCommandHandlerResolver(handler)),
                new TestProfileOperations(), new McpCommandPolicy(), fixture.Authorization, fixture.PathAuthorizer);
            string[] arguments = exitPairMode
                ? ["edit", task.Id.ToString(), "--enabled", "true", "--normal"]
                : ["edit", task.Id.ToString(), "--enabled", "true", "--release-macro", fixture.Release];
            var result = await tools.ExecuteCommandAsync("shortcut", arguments, TestContext.Current.CancellationToken);
            Assert.False(result.IsError is true, JsonSerializer.Serialize(result));
        }
        else
        {
            var result = await fixture.Tools.EditShortcutAsync(task.Id.ToString(), enabled: true,
                runOnPressAndRelease: exitPairMode ? false : null,
                releaseMacroFilePath: exitPairMode ? null : fixture.Release, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Outcome.Success, JsonSerializer.Serialize(result));
        }
        var stored = Assert.Single(fixture.Store.Tasks);
        Assert.True(stored.IsEnabled);
        Assert.Equal(!exitPairMode, stored.RunOnPressAndRelease);
        Assert.Equal(exitPairMode ? string.Empty : fixture.Release, stored.ReleaseMacroFilePath);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = McpTestData.CreateTemporaryDirectory();
        private readonly string _deniedRoot = McpTestData.CreateTemporaryDirectory();
        public string Press => Path.Combine(_root, "press.macro");
        public string Release => Path.Combine(_root, "release.macro");
        public string Denied => Path.Combine(_deniedRoot, "denied.macro");
        public Store Store { get; } = new();
        public ManageShortcut Manage { get; }
        public ShortcutCommandsHarness Commands { get; }
        public McpTaskTools Tools { get; }
        public McpToolAuthorization Authorization { get; }
        public McpPathAuthorizer PathAuthorizer { get; }
        private readonly AutomationTaskMutationGate _gate;
        public Fixture(bool macroRead = true)
        {
            File.WriteAllText(Press, "fixture"); File.WriteAllText(Release, "fixture"); File.WriteAllText(Denied, "fixture");
            var settings = new AppSettings();
            settings.McpSecurity.AllowMacroRead = macroRead;
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [_root]);
            var service = new TestSettingsService(settings);
            var taskAuthorization = new AutomationTaskAuthorization();
            _gate = new AutomationTaskMutationGate(taskAuthorization);
            Manage = new ManageShortcut(Store, Store, _gate);
            Commands = new ShortcutCommandsHarness(ShortcutCommandTestFactory.Create(Manage));
            var schedule = new TestScheduleCommands(); var trigger = new TestTriggerCommands();
            PathAuthorizer = new McpPathAuthorizer(new McpPathPolicy(service));
            Authorization = new McpToolAuthorization(new McpCapabilityPolicy(service), PathAuthorizer, schedule, Commands, trigger, taskAuthorization);
            Tools = new McpTaskTools(schedule, Commands, trigger, Authorization);
        }
        public void Dispose() { Manage.Dispose(); _gate.Dispose(); Directory.Delete(_root, recursive: true); Directory.Delete(_deniedRoot, recursive: true); }
    }

    private sealed class ShortcutCommandsHarness(IShortcutCommands inner) : IShortcutCommands
    {
        public Action? AfterList { get; set; }
        public async Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken)
        {
            var result = await inner.ListAsync(cancellationToken); var action = AfterList; AfterList = null; action?.Invoke(); return result;
        }
        public Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken) => inner.ExecuteAsync(options, cancellationToken);
        public Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken) => inner.RunAsync(taskId, cancellationToken);
    }

    private sealed class Store : IShortcutTaskStore, IShortcutTaskOperations
    {
        public IReadOnlyList<ShortcutTask> Tasks { get; private set; } = [];
        public int CommitCount { get; private set; }
        public int RunCount { get; private set; }
        public Task LoadAsync() => Task.CompletedTask;
        public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default) { Tasks = tasks.Select(AutomationTaskSnapshots.Copy).ToArray(); CommitCount++; return Task.CompletedTask; }
        public Task SaveAsync() => throw new NotSupportedException();
        public void AddTask(ShortcutTask task) => throw new NotSupportedException();
        public void UpdateTask(ShortcutTask task) => throw new NotSupportedException();
        public void RemoveTask(Guid id) => throw new NotSupportedException();
        public void SetTaskEnabled(Guid id, bool enabled) => throw new NotSupportedException();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) { RunCount++; return Task.CompletedTask; }
    }
}
