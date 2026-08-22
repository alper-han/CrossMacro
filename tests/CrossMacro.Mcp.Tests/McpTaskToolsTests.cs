namespace CrossMacro.Mcp.Tests;

public sealed class McpTaskToolsTests
{
    [Fact]
    public async Task TaskTools_ShouldMapScheduleShortcutAndTriggerLists()
    {
        var schedule = new TestScheduleCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 schedule task(s).",
                 new TaskListData<ScheduleTaskData>(1, [new ScheduleTaskData(Guid.NewGuid(), "Daily", true, "Interval", "/tmp/daily.macro", 1, 5, "Minutes", null, null, null, null, null, null)])),
        };
        var shortcut = new TestShortcutCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 shortcut task(s).",
                new TaskListData<ShortcutTaskData>(1, [new ShortcutTaskData(Guid.NewGuid(), "Quick", true, "Ctrl+Alt+Q", "/tmp/quick.macro", 1, false, false, 1, 0, false, null, null, [], null, null)])),
        };
        var trigger = new TestTriggerCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 trigger task(s).",
                new TaskListData<TriggerTaskData>(1, [new TriggerTaskData(Guid.NewGuid(), "Focus", true, "WindowTitle", "Equals", "Editor", "SwitchProfile", "work", null, "OnceOnChange", null, null, null, null)])),
        };
        var tools = McpToolTestFactory.CreateTaskTools(scheduleCliService: schedule, shortcutCliService: shortcut, triggerCliService: trigger);

        var schedules = await tools.ListSchedulesAsync(CancellationToken.None);
        var shortcuts = await tools.ListShortcutsAsync(CancellationToken.None);
        var triggers = await tools.ListTriggersAsync(CancellationToken.None);

        Assert.Equal("Daily", Assert.Single(schedules.Tasks).Name);
        Assert.Equal("Quick", Assert.Single(shortcuts.Tasks).Name);
        Assert.Equal("Focus", Assert.Single(triggers.Tasks).Name);
    }

    [Fact]
    public async Task TaskMutation_ShouldRequireTaskManageCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = McpToolTestFactory.CreateTaskTools(capabilityPolicy: policy);

        var result = await tools.AddScheduleAsync("Daily", "/tmp/daily.macro", cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task TaskMutation_ShouldRequireInputAutomationCapability(string taskType)
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var schedule = new TestScheduleCliService();
        var shortcut = new TestShortcutCliService();
        var trigger = new TestTriggerCliService();
        var tools = McpToolTestFactory.CreateTaskTools(
            capabilityPolicy: policy,
            scheduleCliService: schedule,
            shortcutCliService: shortcut,
            triggerCliService: trigger);

        McpToolOutcome outcome = taskType switch
        {
            "schedule" => (await tools.AddScheduleAsync("Daily", "/tmp/daily.macro", cancellationToken: CancellationToken.None)).Outcome,
            "shortcut" => (await tools.AddShortcutAsync("Quick", "/tmp/quick.macro", "Ctrl+Alt+Q", cancellationToken: CancellationToken.None)).Outcome,
            "trigger" => (await tools.AddTriggerAsync("Focus", "WindowTitle", "Editor", action: "RunMacro", macroPath: "/tmp/focus.macro", cancellationToken: CancellationToken.None)).Outcome,
            _ => throw new ArgumentOutOfRangeException(nameof(taskType), taskType, "Unknown task type."),
        };

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    public async Task TaskRun_ShouldRequireInputAutomationCapability(string taskType)
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var schedule = new TestScheduleCliService();
        var shortcut = new TestShortcutCliService();
        var tools = McpToolTestFactory.CreateTaskTools(capabilityPolicy: policy, scheduleCliService: schedule, shortcutCliService: shortcut);

        McpToolOutcome outcome = taskType is "schedule"
            ? (await tools.RunScheduleAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome
            : (await tools.RunShortcutAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome;

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
    }

    [Fact]
    public async Task ScheduleRun_ShouldAuthorizeTheStoredMacroPathBeforeExecuting()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var taskId = Guid.NewGuid();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        File.WriteAllText(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var schedule = new TestScheduleCliService
            {
                ListResult = CliCommandExecutionResult.Ok(
                    "Loaded 1 schedule task(s).",
                    new TaskListData<ScheduleTaskData>(
                        1,
                        [new ScheduleTaskData(taskId, "Daily", true, "Interval", outsideMacro, 1, 1, "Minutes", null, null, null, null, null, null)])),
            };
            var tools = McpToolTestFactory.CreateTaskTools(
                scheduleCliService: schedule,
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)));

            var result = await tools.RunScheduleAsync(taskId.ToString(), CancellationToken.None);

            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
            Assert.Equal(0, schedule.RunCallCount);
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnableTriggerAsync_ShouldRequireMacroReadForAStoredRunMacro()
    {
        var taskId = Guid.NewGuid();
        var settings = new AppSettings();
        settings.McpSecurity.AllowMacroRead = false;
        var trigger = new TestTriggerCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 trigger task(s).",
                new TaskListData<TriggerTaskData>(
                    1,
                    [new TriggerTaskData(taskId, "Focus", false, "WindowTitle", "Equals", "Editor", "RunMacro", null, "/tmp/focus.macro", "OnceOnChange", null, null, null, null)])),
        };
        var tools = McpToolTestFactory.CreateTaskTools(
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            triggerCliService: trigger);

        var result = await tools.EnableTriggerAsync(taskId.ToString(), CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
        Assert.Equal(0, trigger.ExecuteCallCount);
    }

    [Fact]
    public async Task EnableTriggerAsync_ShouldAuthorizeTheStoredRunMacroPathBeforeExecuting()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var taskId = Guid.NewGuid();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        await File.WriteAllTextAsync(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var trigger = new TestTriggerCliService
            {
                ListResult = CliCommandExecutionResult.Ok(
                    "Loaded 1 trigger task(s).",
                    new TaskListData<TriggerTaskData>(
                        1,
                        [new TriggerTaskData(taskId, "Focus", false, "WindowTitle", "Equals", "Editor", "RunMacro", null, outsideMacro, "OnceOnChange", null, null, null, null)])),
            };
            var tools = McpToolTestFactory.CreateTaskTools(
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)),
                triggerCliService: trigger);

            var result = await tools.EnableTriggerAsync(taskId.ToString(), CancellationToken.None);

            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
            Assert.Equal(0, trigger.ExecuteCallCount);
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }
}
