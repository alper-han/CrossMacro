using CrossMacro.Cli.Commands;
using CrossMacro.Core.Services.Automation;
using CrossMacro.Core.Services.Automation.Scheduling;
using CrossMacro.Core.Services.Automation.Shortcuts;

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
        using var manage = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, runtime, runtime.RunTaskAsync, gate);
        var commands = new ReplacingScheduleCommands(TaskCommandTestFactory.Schedule(manage), () => gate.RunAsync(() =>
        {
            if (replaceProfile)
            {
                _ = gate.AdvanceScopeGeneration();
            }
            runtime.Current = new ScheduledTask { Id = runtime.Current.Id, Name = "Changed", MacroFilePath = paths.Denied, IntervalValue = 5 };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = commands;
        var shortcut = new TestShortcutCommands();
        var trigger = new TestTriggerCommands();
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            ICliCommandHandler handler = run ? TaskCommandHandlers.ScheduleRun(commands) : TaskCommandHandlers.Schedule(commands);
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
        var runtime = new ShortcutRuntime(new ShortcutTask { Name = "Original", MacroFilePath = paths.Allowed, Hotkeys = { "Ctrl+A" } });
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, runtime, runtime.RunTaskAsync, gate);
        var commands = new ReplacingShortcutCommands(TaskCommandTestFactory.Shortcut(manage), () => gate.RunAsync(() =>
        {
            if (replaceProfile)
            {
                _ = gate.AdvanceScopeGeneration();
            }
            runtime.Current = new ShortcutTask { Id = runtime.Current.Id, Name = "Changed", MacroFilePath = paths.Denied, Hotkeys = { "Ctrl+A" } };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = new TestScheduleCommands();
        var shortcut = commands;
        var trigger = new TestTriggerCommands();
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            ICliCommandHandler handler = run ? TaskCommandHandlers.ShortcutRun(commands) : TaskCommandHandlers.Shortcut(commands);
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
            Name = "Original",
            Field = TriggerField.WindowTitle,
            Value = "Editor",
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "profile",
            MacroFilePath = paths.Denied,
        });
        using var manage = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, runtime, gate);
        var commands = new ReplacingTriggerCommands(TaskCommandTestFactory.Trigger(manage), () => gate.RunAsync(() =>
        {
            runtime.Current.Action = TriggerOperation.RunMacro;
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None));
        var schedule = new TestScheduleCommands();
        var shortcut = new TestShortcutCommands();
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, commands, taskAuthorization);
        var id = runtime.Current.Id.ToString();

        if (commandBridge)
        {
            var tools = CreateCommandTools(TaskCommandHandlers.Trigger(commands), authorization, pathAuthorizer);
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
            Field = TriggerField.WindowTitle,
            Value = "Editor",
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "profile",
            MacroFilePath = paths.Denied,
        });
        using var manage = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, runtime, gate);
        var trigger = TaskCommandTestFactory.Trigger(manage);
        var schedule = new TestScheduleCommands();
        var shortcut = new TestShortcutCommands();
        var pathAuthorizer = paths.CreateAuthorizer();
        var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), pathAuthorizer,
            schedule, shortcut, trigger, taskAuthorization);
        var id = runtime.Current.Id.ToString();
        if (commandBridge)
        {
            var tools = CreateCommandTools(TaskCommandHandlers.Trigger(trigger), authorization, pathAuthorizer);
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
        new(McpToolTestFactory.CreateAutomation(), McpToolTestFactory.CreateOperationCoordinator(),
            McpToolTestFactory.CreateCommandLine(new TestCliCommandHandlerResolver(handler)),
            new TestProfileOperations(), authorization, pathAuthorizer);

    private sealed class ReplacingScheduleCommands(IRunnableTaskCommands<ScheduledTask, ScheduleCommand> inner, Func<Task> replace) : IRunnableTaskCommands<ScheduledTask, ScheduleCommand>
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

    private sealed class ScheduleRuntime(ScheduledTask task) : SingleTaskStore<ScheduledTask>(task), IAutomationTaskStore<ScheduledTask>, IScheduledTaskOperations
    {
        public int RunCount { get; private set; }

        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ReplacingShortcutCommands(IRunnableTaskCommands<ShortcutTask, ShortcutCommand> inner, Func<Task> replace) : IRunnableTaskCommands<ShortcutTask, ShortcutCommand>
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

    private sealed class ShortcutRuntime(ShortcutTask task) : SingleTaskStore<ShortcutTask>(task), IAutomationTaskStore<ShortcutTask>, IShortcutTaskOperations
    {
        public int RunCount { get; private set; }

        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ReplacingTriggerCommands(ITaskCommands<TriggerTask, TriggerCommand> inner, Func<Task> replace) : ITaskCommands<TriggerTask, TriggerCommand>
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

    private sealed class TriggerRuntime(TriggerTask task) : SingleTaskStore<TriggerTask>(task), IAutomationTaskStore<TriggerTask>;

    private sealed class MacroPaths : IDisposable
    {
        private readonly TempDirectory _allowedRoot = new();
        private readonly TempDirectory _deniedRoot = new();

        public MacroPaths()
        {
            Allowed = _allowedRoot.WriteFile("allowed.macro");
            Denied = _deniedRoot.WriteFile("denied.macro");
        }

        public string Allowed { get; }

        public string Denied { get; }

        public McpPathAuthorizer CreateAuthorizer()
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [_allowedRoot.Path]);
            return new McpPathAuthorizer(new McpPathPolicy(new TestSettingsService(settings)));
        }

        public void Dispose()
        {
            _allowedRoot.Dispose();
            _deniedRoot.Dispose();
        }
    }
}
