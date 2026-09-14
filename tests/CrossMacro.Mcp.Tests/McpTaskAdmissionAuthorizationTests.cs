using CrossMacro.Cli.Commands;

namespace CrossMacro.Mcp.Tests;

public sealed class McpTaskAdmissionAuthorizationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Schedule_AuthorizesTheActuallySelectedPathBeforeCommitOrRun(bool commandBridge, bool replaceProfile, bool run)
    {
        using var paths = new MacroPaths();
        var taskAuthorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(taskAuthorization);
        var runtime = new ScheduleRuntime(new ScheduledTask { Name = "Original", MacroFilePath = paths.Allowed, IntervalValue = 5 });
        using var manage = new ManageSchedule(runtime, runtime, gate);
        var commands = new ReplacingScheduleCommands(new ScheduleCommands(manage), () => gate.RunAsync(() =>
        {
            if (replaceProfile)
            {
                _ = gate.AdvanceScopeGeneration();
            }
            runtime.Current = new ScheduledTask { Id = runtime.Current.Id, Name = "Changed", MacroFilePath = paths.Denied, IntervalValue = 5 };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = commands;
        var shortcut = new ShortcutCommandTestAdapter(new TestShortcutCliService());
        var trigger = new TriggerCommandTestAdapter(new TestTriggerCliService());
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            var cli = new ScheduleCliService(commands);
            ICliCommandHandler handler = run ? new ScheduleRunCommandHandler(cli) : new ScheduleCommandHandler(cli);
            var tools = CreateCommandTools(handler, authorization, pathAuthorizer);
            var result = await tools.ExecuteCommandAsync("schedule", [run ? "run" : "enable", id], CancellationToken.None);
            Assert.True(result.IsError);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.DoesNotContain("scopeGeneration", Assert.IsType<JsonElement>(result.StructuredContent).GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var tools = new McpTaskTools(schedule, shortcut, trigger, authorization);
            var result = run
                ? await tools.RunScheduleAsync(id, CancellationToken.None)
                : await tools.EnableScheduleAsync(id, CancellationToken.None);
            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        }

        Assert.Equal(0, runtime.RunCount);
        Assert.Equal(0, runtime.CommitCount);
        Assert.False(runtime.Current.IsEnabled);
        Assert.Equal(paths.Denied, runtime.Current.MacroFilePath);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Shortcut_AuthorizesTheActuallySelectedPathBeforeCommitOrRun(bool commandBridge, bool replaceProfile, bool run)
    {
        using var paths = new MacroPaths();
        var taskAuthorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(taskAuthorization);
        var runtime = new ShortcutRuntime(new ShortcutTask { Name = "Original", MacroFilePath = paths.Allowed, HotkeyString = "Ctrl+A" });
        using var manage = new ManageShortcut(runtime, runtime, gate);
        var commands = new ReplacingShortcutCommands(new ShortcutCommands(manage), () => gate.RunAsync(() =>
        {
            if (replaceProfile)
            {
                _ = gate.AdvanceScopeGeneration();
            }
            runtime.Current = new ShortcutTask { Id = runtime.Current.Id, Name = "Changed", MacroFilePath = paths.Denied, HotkeyString = "Ctrl+A" };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = new ScheduleCommandTestAdapter(new TestScheduleCliService());
        var shortcut = commands;
        var trigger = new TriggerCommandTestAdapter(new TestTriggerCliService());
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            var cli = new ShortcutCliService(commands);
            ICliCommandHandler handler = run ? new ShortcutRunCommandHandler(cli) : new ShortcutCommandHandler(cli);
            var tools = CreateCommandTools(handler, authorization, pathAuthorizer);
            var result = await tools.ExecuteCommandAsync("shortcut", [run ? "run" : "enable", id], CancellationToken.None);
            Assert.True(result.IsError);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.DoesNotContain("scopeGeneration", Assert.IsType<JsonElement>(result.StructuredContent).GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var tools = new McpTaskTools(schedule, shortcut, trigger, authorization);
            var result = run
                ? await tools.RunShortcutAsync(id, CancellationToken.None)
                : await tools.EnableShortcutAsync(id, CancellationToken.None);
            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        }

        Assert.Equal(0, runtime.RunCount);
        Assert.Equal(0, runtime.CommitCount);
        Assert.False(runtime.Current.IsEnabled);
        Assert.Equal(paths.Denied, runtime.Current.MacroFilePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trigger_RechecksEffectivePath_WhenProfileActionBecomesRunMacro(bool commandBridge)
    {
        using var paths = new MacroPaths();
        var taskAuthorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(taskAuthorization);
        var runtime = new TriggerRuntime(new TriggerTask
        {
            Name = "Original", Field = TriggerField.WindowTitle, Value = "Editor",
            Action = TriggerOperation.SwitchProfile, TargetProfileId = "profile", MacroFilePath = paths.Denied,
        });
        using var manage = new ManageTrigger(runtime, runtime, gate);
        var commands = new ReplacingTriggerCommands(new TriggerCommands(manage), () => gate.RunAsync(() =>
        {
            runtime.Current.Action = TriggerOperation.RunMacro;
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = new ScheduleCommandTestAdapter(new TestScheduleCliService());
        var shortcut = new ShortcutCommandTestAdapter(new TestShortcutCliService());
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, commands, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            var tools = CreateCommandTools(new TriggerCommandHandler(new TriggerCliService(commands)), authorization, pathAuthorizer);
            var result = await tools.ExecuteCommandAsync("trigger", ["enable", id], CancellationToken.None);
            Assert.True(result.IsError);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        else
        {
            var tools = new McpTaskTools(schedule, shortcut, commands, authorization);
            var result = await tools.EnableTriggerAsync(id, CancellationToken.None);
            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        }
        Assert.Equal(0, runtime.CommitCount);
        Assert.False(runtime.Current.IsEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TriggerEdit_AuthorizesTheFinalActionAndItsExistingMacroPath(bool commandBridge)
    {
        using var paths = new MacroPaths();
        var taskAuthorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(taskAuthorization);
        var runtime = new TriggerRuntime(new TriggerTask
        {
            Field = TriggerField.WindowTitle, Value = "Editor", Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "profile", MacroFilePath = paths.Denied,
        });
        using var manage = new ManageTrigger(runtime, runtime, gate);
        var trigger = new TriggerCommands(manage);
        var schedule = new ScheduleCommandTestAdapter(new TestScheduleCliService());
        var shortcut = new ShortcutCommandTestAdapter(new TestShortcutCliService());
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();
        if (commandBridge)
        {
            var tools = CreateCommandTools(new TriggerCommandHandler(new TriggerCliService(trigger)), authorization, pathAuthorizer);
            var result = await tools.ExecuteCommandAsync("trigger", ["edit", id, "--action", "RunMacro"], CancellationToken.None);
            Assert.True(result.IsError);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        else
        {
            var tools = new McpTaskTools(schedule, shortcut, trigger, authorization);
            var result = await tools.EditTriggerAsync(id, action: "RunMacro", cancellationToken: CancellationToken.None);
            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
        }
        Assert.Equal(0, runtime.CommitCount);
        Assert.Equal(TriggerOperation.SwitchProfile, runtime.Current.Action);
    }

    private static McpCommandTools CreateCommandTools(ICliCommandHandler handler, McpToolAuthorization authorization, McpPathAuthorizer pathAuthorizer) =>
        new(McpToolTestFactory.CreateMacroExecutionService(), McpToolTestFactory.CreateOperationCoordinator(),
            McpToolTestFactory.CreateRunScriptExecutionService(), McpToolTestFactory.CreateRecordExecutionService(),
            McpToolTestFactory.CreatePreflightService(), new CliCommandExecutor(new TestCliCommandHandlerResolver(handler)),
            new TestProfileOperations(), new McpCommandPolicy(), authorization, pathAuthorizer);

    private sealed class ReplacingScheduleCommands(IScheduleCommands inner, Func<Task> replace) : IScheduleCommands
    {
        private bool _replaced;
        public async Task<TaskCommandResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken)
        {
            var result = await inner.ListAsync(cancellationToken);
            if (!_replaced)
            {
                _replaced = true;
                await replace();
            }
            return result;
        }
        public Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken) => inner.ExecuteAsync(options, cancellationToken);
        public Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken) => inner.RunAsync(taskId, cancellationToken);
    }

    private sealed class ScheduleRuntime(ScheduledTask task) : IScheduledTaskStore, IScheduledTaskOperations
    {
        public ScheduledTask Current { get; set; } = task;
        public int CommitCount { get; private set; }
        public int RunCount { get; private set; }
        public IReadOnlyList<ScheduledTask> Tasks => [Current];
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => throw new NotSupportedException();
        public Task CommitAsync(IReadOnlyList<ScheduledTask> tasks, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }
        public void AddTask(ScheduledTask task) => throw new NotSupportedException();
        public void UpdateTask(ScheduledTask task) => throw new NotSupportedException();
        public void RemoveTask(Guid id) => throw new NotSupportedException();
        public void SetTaskEnabled(Guid id, bool enabled) => throw new NotSupportedException();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ReplacingShortcutCommands(IShortcutCommands inner, Func<Task> replace) : IShortcutCommands
    {
        private bool _replaced;
        public async Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken)
        {
            var result = await inner.ListAsync(cancellationToken);
            if (!_replaced)
            {
                _replaced = true;
                await replace();
            }
            return result;
        }
        public Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken) => inner.ExecuteAsync(options, cancellationToken);
        public Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken) => inner.RunAsync(taskId, cancellationToken);
    }

    private sealed class ShortcutRuntime(ShortcutTask task) : IShortcutTaskStore, IShortcutTaskOperations
    {
        public ShortcutTask Current { get; set; } = task;
        public int CommitCount { get; private set; }
        public int RunCount { get; private set; }
        public IReadOnlyList<ShortcutTask> Tasks => [Current];
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => throw new NotSupportedException();
        public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }
        public void AddTask(ShortcutTask task) => throw new NotSupportedException();
        public void UpdateTask(ShortcutTask task) => throw new NotSupportedException();
        public void RemoveTask(Guid id) => throw new NotSupportedException();
        public void SetTaskEnabled(Guid id, bool enabled) => throw new NotSupportedException();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ReplacingTriggerCommands(ITriggerCommands inner, Func<Task> replace) : ITriggerCommands
    {
        private bool _replaced;
        public async Task<TaskCommandResult<TriggerTask>> ListAsync(CancellationToken cancellationToken)
        {
            var result = await inner.ListAsync(cancellationToken);
            if (!_replaced)
            {
                _replaced = true;
                await replace();
            }
            return result;
        }
        public Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken) => inner.ExecuteAsync(options, cancellationToken);
    }

    private sealed class TriggerRuntime(TriggerTask task) : ITriggerTaskStore, ITriggerTaskOperations
    {
        public TriggerTask Current { get; set; } = task;
        public int CommitCount { get; private set; }
        public IReadOnlyList<TriggerTask> Tasks => [Current];
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => throw new NotSupportedException();
        public Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }
        public void AddTask(TriggerTask task) => throw new NotSupportedException();
        public void UpdateTask(TriggerTask task) => throw new NotSupportedException();
        public void RemoveTask(Guid id) => throw new NotSupportedException();
        public void SetTaskEnabled(Guid id, bool enabled) => throw new NotSupportedException();
    }

    private sealed class MacroPaths : IDisposable
    {
        private readonly string _allowedRoot = McpTestData.CreateTemporaryDirectory();
        private readonly string _deniedRoot = McpTestData.CreateTemporaryDirectory();
        public string Allowed { get; }
        public string Denied { get; }
        public MacroPaths()
        {
            Allowed = Path.Combine(_allowedRoot, "allowed.macro");
            Denied = Path.Combine(_deniedRoot, "denied.macro");
            File.WriteAllText(Allowed, "fixture");
            File.WriteAllText(Denied, "fixture");
        }
        public McpPathAuthorizer CreateAuthorizer()
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [_allowedRoot]);
            return new McpPathAuthorizer(new McpPathPolicy(new TestSettingsService(settings)));
        }
        public void Dispose()
        {
            Directory.Delete(_allowedRoot, recursive: true);
            Directory.Delete(_deniedRoot, recursive: true);
        }
    }
}
