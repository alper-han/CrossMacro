namespace CrossMacro.Mcp.Tests;

public sealed class McpTaskToolsTests
{
    [Fact]
    public async Task TaskTools_ShouldMapScheduleShortcutAndTriggerLists()
    {
        var schedule = new TestScheduleCommands
        {
            ListResult = new TaskCommandResult<ScheduledTask>(
                Success: true, "Loaded 1 schedule task(s).", [],
                Tasks:
                [
                    new ScheduledTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Daily",
                        IsEnabled = true,
                        Type = ScheduleType.Interval,
                        MacroFilePath = "/tmp/daily.macro",
                        PlaybackSpeed = 1,
                        IntervalValue = 5,
                        IntervalUnit = IntervalUnit.Minutes,
                        ScheduledDateTime = null,
                        WeeklyDays = ScheduleDays.None,
                        WeeklyTime = TimeSpan.Zero,
                        NextRunTime = null,
                        LastRunTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var shortcut = new TestShortcutCommands
        {
            ListResult = new TaskCommandResult<ShortcutTask>(
                Success: true, "Loaded 1 shortcut task(s).", [],
                Tasks:
                [
                    new ShortcutTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Quick",
                        IsEnabled = true,
                        HotkeyString = "Ctrl+Alt+Q",
                        MacroFilePath = "/tmp/quick.macro",
                        PlaybackSpeed = 1,
                        LoopEnabled = false,
                        RunWhileHeld = false,
                        RepeatCount = 1,
                        RepeatDelayMs = 0,
                        UseRandomRepeatDelay = false,
                        RepeatDelayMinMs = 0,
                        RepeatDelayMaxMs = 0,
                        LastTriggeredTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var trigger = new TestTriggerCommands
        {
            ListResult = new TaskCommandResult<TriggerTask>(
                Success: true, "Loaded 1 trigger task(s).", [],
                Tasks:
                [
                    new TriggerTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Focus",
                        IsEnabled = true,
                        Field = TriggerField.WindowTitle,
                        MatchMode = TriggerMatchMode.Equals,
                        Value = "Editor",
                        Action = TriggerOperation.SwitchProfile,
                        TargetProfileId = "work",
                        MacroFilePath = string.Empty,
                        FireMode = TriggerFireMode.OnceOnChange,
                        CooldownMs = null,
                        DebounceMs = null,
                        LastTriggeredTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var tools = McpToolTestFactory.CreateTaskTools(scheduleCommands: schedule, shortcutCommands: shortcut, triggerCommands: trigger);

        var schedules = await tools.ListSchedulesAsync(CancellationToken.None);
        var shortcuts = await tools.ListShortcutsAsync(CancellationToken.None);
        var triggers = await tools.ListTriggersAsync(CancellationToken.None);

        Assert.Equal("Daily", Assert.Single(schedules.Tasks).Name);
        Assert.Equal("Quick", Assert.Single(shortcuts.Tasks).Name);
        Assert.Equal("Focus", Assert.Single(triggers.Tasks).Name);
    }

    [Fact]
    public async Task TaskLists_ShouldRedactMacroPathsWithoutMacroReadCapability()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowMacroRead = false;
        var schedule = new TestScheduleCommands
        {
            ListResult = new TaskCommandResult<ScheduledTask>(
                Success: true, "Loaded 1 schedule task(s).", [],
                Tasks:
                [
                    new ScheduledTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Daily",
                        IsEnabled = true,
                        Type = ScheduleType.Interval,
                        MacroFilePath = "/private/schedule.macro",
                        PlaybackSpeed = 1,
                        IntervalValue = 1,
                        IntervalUnit = IntervalUnit.Minutes,
                        ScheduledDateTime = null,
                        WeeklyDays = ScheduleDays.None,
                        WeeklyTime = TimeSpan.Zero,
                        NextRunTime = null,
                        LastRunTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var shortcut = new TestShortcutCommands
        {
            ListResult = new TaskCommandResult<ShortcutTask>(
                Success: true, "Loaded 1 shortcut task(s).", [],
                Tasks:
                [
                    new ShortcutTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Quick",
                        IsEnabled = true,
                        HotkeyString = "Ctrl+Alt+Q",
                        MacroFilePath = "/private/shortcut.macro",
                        PlaybackSpeed = 1,
                        LoopEnabled = false,
                        RunWhileHeld = false,
                        RepeatCount = 1,
                        RepeatDelayMs = 0,
                        UseRandomRepeatDelay = false,
                        RepeatDelayMinMs = 0,
                        RepeatDelayMaxMs = 0,
                        LastTriggeredTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var trigger = new TestTriggerCommands
        {
            ListResult = new TaskCommandResult<TriggerTask>(
                Success: true, "Loaded 1 trigger task(s).", [],
                Tasks:
                [
                    new TriggerTask
                    {
                        Id = Guid.NewGuid(),
                        Name = "Focus",
                        IsEnabled = true,
                        Field = TriggerField.WindowTitle,
                        MatchMode = TriggerMatchMode.Equals,
                        Value = "Editor",
                        Action = TriggerOperation.RunMacro,
                        TargetProfileId = string.Empty,
                        MacroFilePath = "/private/trigger.macro",
                        FireMode = TriggerFireMode.OnceOnChange,
                        CooldownMs = null,
                        DebounceMs = null,
                        LastTriggeredTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var tools = McpToolTestFactory.CreateTaskTools(
            scheduleCommands: schedule,
            shortcutCommands: shortcut,
            triggerCommands: trigger,
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)));

        var schedules = await tools.ListSchedulesAsync(CancellationToken.None);
        var shortcuts = await tools.ListShortcutsAsync(CancellationToken.None);
        var triggers = await tools.ListTriggersAsync(CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(schedules.Tasks).MacroFilePath);
        Assert.Equal(string.Empty, Assert.Single(shortcuts.Tasks).MacroFilePath);
        Assert.Null(Assert.Single(triggers.Tasks).MacroFilePath);
    }

    [Fact]
    public async Task TaskMutation_ShouldRequireTaskManageCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(restricted: true);
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
        var schedule = new TestScheduleCommands();
        var shortcut = new TestShortcutCommands();
        var trigger = new TestTriggerCommands();
        var tools = McpToolTestFactory.CreateTaskTools(
            capabilityPolicy: policy,
            scheduleCommands: schedule,
            shortcutCommands: shortcut,
            triggerCommands: trigger);

        McpToolOutcome outcome = taskType switch
        {
            "schedule" => (await tools.AddScheduleAsync("Daily", "/tmp/daily.macro", cancellationToken: CancellationToken.None)).Outcome,
            "shortcut" => (await tools.AddShortcutAsync("Quick", "/tmp/quick.macro", "Ctrl+Alt+Q", cancellationToken: CancellationToken.None)).Outcome,
            "trigger" => (await tools.AddTriggerAsync("Focus", "WindowTitle", "Editor", action: "RunMacro", macroPath: "/tmp/focus.macro", cancellationToken: CancellationToken.None)).Outcome,
            _ => throw new ArgumentOutOfRangeException(nameof(taskType), taskType, "Unknown task type."),
        };

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
        Assert.Equal(0, schedule.ExecuteCallCount);
        Assert.Equal(0, shortcut.ExecuteCallCount);
        Assert.Equal(0, trigger.ExecuteCallCount);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    public async Task TaskRun_ShouldRequireInputAutomationCapability(string taskType)
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var schedule = new TestScheduleCommands();
        var shortcut = new TestShortcutCommands();
        var tools = McpToolTestFactory.CreateTaskTools(capabilityPolicy: policy, scheduleCommands: schedule, shortcutCommands: shortcut);

        McpToolOutcome outcome = taskType is "schedule"
            ? (await tools.RunScheduleAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome
            : (await tools.RunShortcutAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome;

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
        Assert.Equal(0, schedule.RunCallCount);
        Assert.Equal(0, shortcut.RunCallCount);
    }

    [Fact]
    public async Task ScheduleRun_ShouldAuthorizeTheStoredMacroPathBeforeExecuting()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var taskId = Guid.NewGuid();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        await File.WriteAllTextAsync(outsideMacro, "macro", CancellationToken.None);
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var schedule = new TestScheduleCommands
            {
                ListResult = new TaskCommandResult<ScheduledTask>(
                    Success: true, "Loaded 1 schedule task(s).", [],
                    Tasks:
                    [
                        new ScheduledTask
                        {
                            Id = taskId,
                            Name = "Daily",
                            IsEnabled = true,
                            Type = ScheduleType.Interval,
                            MacroFilePath = outsideMacro,
                            PlaybackSpeed = 1,
                            IntervalValue = 1,
                            IntervalUnit = IntervalUnit.Minutes,
                            ScheduledDateTime = null,
                            WeeklyDays = ScheduleDays.None,
                            WeeklyTime = TimeSpan.Zero,
                            NextRunTime = null,
                            LastRunTime = null,
                            LastStatus = null,
                        },
                    ]),
            };
            var tools = McpToolTestFactory.CreateTaskTools(
                scheduleCommands: schedule,
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
        var trigger = new TestTriggerCommands
        {
            ListResult = new TaskCommandResult<TriggerTask>(
                Success: true, "Loaded 1 trigger task(s).", [],
                Tasks:
                [
                    new TriggerTask
                    {
                        Id = taskId,
                        Name = "Focus",
                        IsEnabled = false,
                        Field = TriggerField.WindowTitle,
                        MatchMode = TriggerMatchMode.Equals,
                        Value = "Editor",
                        Action = TriggerOperation.RunMacro,
                        TargetProfileId = string.Empty,
                        MacroFilePath = "/tmp/focus.macro",
                        FireMode = TriggerFireMode.OnceOnChange,
                        CooldownMs = null,
                        DebounceMs = null,
                        LastTriggeredTime = null,
                        LastStatus = null,
                    },
                ]),
        };
        var tools = McpToolTestFactory.CreateTaskTools(
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            triggerCommands: trigger);

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
        await File.WriteAllTextAsync(outsideMacro, "macro", CancellationToken.None);
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var trigger = new TestTriggerCommands
            {
                ListResult = new TaskCommandResult<TriggerTask>(
                    Success: true, "Loaded 1 trigger task(s).", [],
                    Tasks:
                    [
                        new TriggerTask
                        {
                            Id = taskId,
                            Name = "Focus",
                            IsEnabled = false,
                            Field = TriggerField.WindowTitle,
                            MatchMode = TriggerMatchMode.Equals,
                            Value = "Editor",
                            Action = TriggerOperation.RunMacro,
                            TargetProfileId = string.Empty,
                            MacroFilePath = outsideMacro,
                            FireMode = TriggerFireMode.OnceOnChange,
                            CooldownMs = null,
                            DebounceMs = null,
                            LastTriggeredTime = null,
                            LastStatus = null,
                        },
                    ]),
            };
            var tools = McpToolTestFactory.CreateTaskTools(
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)),
                triggerCommands: trigger);

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
