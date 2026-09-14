namespace CrossMacro.Application.Tests.Automation;

public sealed class TaskCommandScopeTests
{
    [Theory]
    [InlineData(ScheduleCommandAction.Add)]
    [InlineData(ScheduleCommandAction.Edit)]
    [InlineData(ScheduleCommandAction.Remove)]
    [InlineData(ScheduleCommandAction.Enable)]
    [InlineData(ScheduleCommandAction.Disable)]
    public async Task ScheduleCommand_PreservesItsListScope_AndMapsScopeConflict(ScheduleCommandAction action)
    {
        const long originalScope = 27;
        var task = new ScheduledTask { Name = "Task", MacroFilePath = "macro", IntervalValue = 5 };
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([task], originalScope));
        _ = workflow.AddAsync(Arg.Any<ScheduledTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScheduledTask>(new TaskScopeConflictException()));
        _ = workflow.UpdateAsync(Arg.Any<ScheduledTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScheduledTask>(new TaskScopeConflictException()));
        _ = workflow.RemoveAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScheduledTask>(new TaskScopeConflictException()));
        _ = workflow.SetEnabledAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScheduledTask>(new TaskScopeConflictException()));
        var commands = new ScheduleCommands(workflow);

        var result = await commands.ExecuteAsync(new ScheduleCommand(action, TaskId: task.Id.ToString(), Name: "Draft", MacroFilePath: "macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TaskScopeConflictException.ConflictMessage, result.Message);
        Assert.Equal("Task", task.Name);
    }

    [Fact]
    public async Task ScheduleRun_PreservesItsListScope_AndMapsScopeConflict()
    {
        var task = new ScheduledTask { Name = "Task", MacroFilePath = "macro", IntervalValue = 5 };
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([task], 27));
        _ = workflow.RunAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == 27), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TaskScopeConflictException()));

        var result = await new ScheduleCommands(workflow).RunAsync(task.Id.ToString(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TaskScopeConflictException.ConflictMessage, result.Message);
    }

    [Theory]
    [InlineData(ShortcutCommandAction.Add)]
    [InlineData(ShortcutCommandAction.Edit)]
    [InlineData(ShortcutCommandAction.Remove)]
    [InlineData(ShortcutCommandAction.Enable)]
    [InlineData(ShortcutCommandAction.Disable)]
    [InlineData(ShortcutCommandAction.Bind)]
    public async Task ShortcutCommand_PreservesItsListScope_AndMapsScopeConflict(ShortcutCommandAction action)
    {
        const long originalScope = 27;
        var task = new ShortcutTask { Name = "Task", MacroFilePath = "macro", HotkeyString = "Ctrl+A" };
        var workflow = Substitute.For<IManageShortcut>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([task], originalScope));
        _ = workflow.AddAsync(Arg.Any<ShortcutTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ShortcutTask>(new TaskScopeConflictException()));
        _ = workflow.UpdateAsync(Arg.Any<ShortcutTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ShortcutTask>(new TaskScopeConflictException()));
        _ = workflow.RemoveAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ShortcutTask>(new TaskScopeConflictException()));
        _ = workflow.SetEnabledAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ShortcutTask>(new TaskScopeConflictException()));
        var commands = new ShortcutCommands(workflow);

        var result = await commands.ExecuteAsync(new ShortcutCommand(action, TaskId: task.Id.ToString(), Name: "Draft", MacroFilePath: "macro", Hotkey: "Ctrl+B"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TaskScopeConflictException.ConflictMessage, result.Message);
        Assert.Equal("Task", task.Name);
    }

    [Fact]
    public async Task ShortcutRun_PreservesItsListScope_AndMapsScopeConflict()
    {
        var task = new ShortcutTask { Name = "Task", MacroFilePath = "macro", HotkeyString = "Ctrl+A" };
        var workflow = Substitute.For<IManageShortcut>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([task], 27));
        _ = workflow.RunAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == 27), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TaskScopeConflictException()));

        var result = await new ShortcutCommands(workflow).RunAsync(task.Id.ToString(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TaskScopeConflictException.ConflictMessage, result.Message);
    }

    [Theory]
    [InlineData(TriggerCommandAction.Add)]
    [InlineData(TriggerCommandAction.Edit)]
    [InlineData(TriggerCommandAction.Remove)]
    [InlineData(TriggerCommandAction.Enable)]
    [InlineData(TriggerCommandAction.Disable)]
    public async Task TriggerCommand_PreservesItsListScope_AndMapsScopeConflict(TriggerCommandAction action)
    {
        const long originalScope = 27;
        var task = new TriggerTask { Name = "Task", Field = TriggerField.WindowTitle, Value = "Window", Action = TriggerOperation.RunMacro, MacroFilePath = "macro" };
        var workflow = Substitute.For<IManageTrigger>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>([task], originalScope));
        _ = workflow.AddAsync(Arg.Any<TriggerTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TriggerTask>(new TaskScopeConflictException()));
        _ = workflow.UpdateAsync(Arg.Any<TriggerTask>(), originalScope, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TriggerTask>(new TaskScopeConflictException()));
        _ = workflow.RemoveAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TriggerTask>(new TaskScopeConflictException()));
        _ = workflow.SetEnabledAsync(Arg.Is<TaskRequest>(request => request.ExpectedScopeGeneration == originalScope), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TriggerTask>(new TaskScopeConflictException()));
        var commands = new TriggerCommands(workflow);

        var result = await commands.ExecuteAsync(new TriggerCommand(action, TaskId: task.Id.ToString(), Name: "Draft", MacroFilePath: "macro"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(TaskScopeConflictException.ConflictMessage, result.Message);
        Assert.Equal("Task", task.Name);
    }

}
