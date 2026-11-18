namespace CrossMacro.Application.Tests.Automation;

public sealed class TaskCommandEnablementTests
{
    [Fact]
    public async Task TriggerAdd_WithEnabledAndInvalidConfiguration_IsRejectedInsteadOfSilentlyDisabled()
    {
        var workflow = Substitute.For<IManageTrigger>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>([], 1));

        var result = await new TriggerCommands(workflow).ExecuteAsync(
            new TriggerCommand(TriggerCommandAction.Add, Name: "T", Field: TriggerField.None, Operation: TriggerOperation.SwitchProfile, Enabled: true),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _ = await workflow.DidNotReceive().AddAsync(Arg.Any<TriggerTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerEdit_WithEnabledAndInvalidConfiguration_IsRejected()
    {
        var existing = new TriggerTask { Name = "T", Field = TriggerField.None, Action = TriggerOperation.SwitchProfile, TargetProfileId = "dev" };
        var workflow = Substitute.For<IManageTrigger>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>([existing], 1));

        var result = await new TriggerCommands(workflow).ExecuteAsync(
            new TriggerCommand(TriggerCommandAction.Edit, TaskId: existing.Id.ToString(), TargetProfileId: string.Empty, Enabled: true),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _ = await workflow.DidNotReceive().UpdateAsync(Arg.Any<TriggerTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAdd_WithEnabledAndNoMacro_IsRejectedInsteadOfSilentlyDisabled()
    {
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([], 1));

        var result = await new ScheduleCommands(workflow).ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Add, Name: "S", Interval: "5s", Enabled: true),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _ = await workflow.DidNotReceive().AddAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleEdit_WithEnabledOnWeeklyTaskWithoutDays_IsRejected()
    {
        var existing = new ScheduledTask { Name = "S", MacroFilePath = "macro", Type = ScheduleType.Weekly, WeeklyDays = ScheduleDays.None };
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([existing], 1));

        var result = await new ScheduleCommands(workflow).ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Edit, TaskId: existing.Id.ToString(), Enabled: true),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _ = await workflow.DidNotReceive().UpdateAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortcutEnable_ReportsTheCommittedTask()
    {
        var existing = new ShortcutTask { Name = "Stale", MacroFilePath = "macro", Hotkeys = { "Ctrl+A" } };
        var committed = AutomationTaskSnapshots.Copy(existing);
        committed.Name = "Committed";
        committed.IsEnabled = true;
        var workflow = Substitute.For<IManageShortcut>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([existing], 1));
        _ = workflow.SetEnabledAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>()).Returns(committed);

        var result = await ShortcutCommandTestFactory.Create(workflow).ExecuteAsync(
            new ShortcutCommand(ShortcutCommandAction.Enable, TaskId: existing.Id.ToString()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(committed, result.Task);
        Assert.Contains("Committed", result.Message, StringComparison.Ordinal);
        Assert.False(existing.IsEnabled);
    }

    [Fact]
    public async Task ScheduleEnable_ReportsTheCommittedTask()
    {
        var existing = new ScheduledTask { Name = "Stale", MacroFilePath = "macro", Type = ScheduleType.Interval, IntervalValue = 5 };
        var committed = AutomationTaskSnapshots.Copy(existing);
        committed.Name = "Committed";
        committed.IsEnabled = true;
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([existing], 1));
        _ = workflow.SetEnabledAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>()).Returns(committed);

        var result = await new ScheduleCommands(workflow).ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Enable, TaskId: existing.Id.ToString()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(committed, result.Task);
        Assert.False(existing.IsEnabled);
    }

    [Theory]
    [InlineData("5", 5, IntervalUnit.Seconds)]
    [InlineData("5s", 5, IntervalUnit.Seconds)]
    [InlineData("7m", 7, IntervalUnit.Minutes)]
    [InlineData("3H", 3, IntervalUnit.Hours)]
    public async Task ScheduleAdd_ParsesIntervalSuffix(string interval, int expectedValue, IntervalUnit expectedUnit)
    {
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([], 1));
        _ = workflow.AddAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<ScheduledTask>());

        var result = await new ScheduleCommands(workflow).ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Add, Name: "S", MacroFilePath: "macro", Interval: interval),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(expectedValue, result.Task!.IntervalValue);
        Assert.Equal(expectedUnit, result.Task.IntervalUnit);
    }

    [Theory]
    [InlineData("5x")]
    [InlineData("0s")]
    [InlineData("abc")]
    public async Task ScheduleAdd_RejectsInvalidInterval(string interval)
    {
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([], 1));

        var result = await new ScheduleCommands(workflow).ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Add, Name: "S", MacroFilePath: "macro", Interval: interval),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ScheduleAdd_WeeklyAppliesTimeAndRejectsUnparsableTime()
    {
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([], 1));
        _ = workflow.AddAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<ScheduledTask>());
        var commands = new ScheduleCommands(workflow);

        var ok = await commands.ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Add, Name: "S", MacroFilePath: "macro", Weekly: "mon,fri", Time: "07:30"),
            TestContext.Current.CancellationToken);
        var bad = await commands.ExecuteAsync(
            new ScheduleCommand(ScheduleCommandAction.Add, Name: "S", MacroFilePath: "macro", Weekly: "mon", Time: "not-a-time"),
            TestContext.Current.CancellationToken);

        Assert.True(ok.Success);
        Assert.Equal(new TimeSpan(7, 30, 0), ok.Task!.WeeklyTime);
        Assert.Equal(ScheduleDays.Monday | ScheduleDays.Friday, ok.Task.WeeklyDays);
        Assert.False(bad.Success);
    }
}
