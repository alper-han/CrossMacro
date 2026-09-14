
namespace CrossMacro.Cli.Tests;

public sealed class ScheduleCommandBehaviorTests
{
    [Fact]
    public async Task EditWithInvalidSchedule_DoesNotModifyTheListedTask()
    {
        var task = new ScheduledTask { Name = "Original", MacroFilePath = "/tmp/original.macro" };
        var workflow = CreateWorkflow();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([task]));
        var service = new ScheduleCommandHandler(new ScheduleCommands(workflow));

        var result = await service.ExecuteAsync(new ScheduleCliOptions(
            ScheduleCliAction.Edit, TaskId: task.Id.ToString(), Name: "Changed", MacroFilePath: "/tmp/changed.macro", Interval: "invalid"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Original", task.Name);
        Assert.Equal("/tmp/original.macro", task.MacroFilePath);
        _ = await workflow.DidNotReceive().UpdateAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro",
                IsEnabled = true,
            },
        }));

        var service = new ScheduleListCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleListCliOptions(), CancellationToken.None);

        Assert.True(result.Success);
        _ = await scheduler.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ListAsync_WhenTaskIsWeekly_ReturnsWeeklyFields()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Weekly task",
                MacroFilePath = "/tmp/a.macro",
                Type = ScheduleType.Weekly,
                WeeklyDays = ScheduleDays.Weekdays,
                WeeklyTime = new TimeSpan(9, 30, 0),
            },
        }));

        var service = new ScheduleListCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleListCliOptions(), CancellationToken.None);

        var taskList = Assert.IsType<TaskListData<ScheduleTaskData>>(result.Data);
        var task = Assert.Single(taskList.Tasks);
        Assert.Equal("Weekdays", task.WeeklyDays);
        Assert.Equal("09:30:00", task.WeeklyTime);
    }

    [Fact]
    public async Task ListAsync_WhenTaskIsNotWeekly_OmitsWeeklyFields()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Interval task",
                MacroFilePath = "/tmp/a.macro",
                Type = ScheduleType.Interval,
            },
        }));

        var service = new ScheduleListCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleListCliOptions(), CancellationToken.None);

        var taskList = Assert.IsType<TaskListData<ScheduleTaskData>>(result.Data);
        var task = Assert.Single(taskList.Tasks);
        Assert.Equal(1.0, task.PlaybackSpeed);
        Assert.Equal(30, task.IntervalValue);
        Assert.Equal("Seconds", task.IntervalUnit);
        Assert.Null(task.WeeklyDays);
        Assert.Null(task.WeeklyTime);
    }

    [Fact]
    public async Task RunAsync_WithInvalidGuid_ReturnsInvalidArguments()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>()));

        var service = new ScheduleRunCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleRunCliOptions("invalid-guid"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_WithMissingTask_ReturnsInvalidArguments()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>()));

        var service = new ScheduleRunCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleRunCliOptions("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        _ = await scheduler.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WithExistingTask_RunsTask()
    {
        var id = new Guid(0x11111111, 0x1111, 0x1111, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = id,
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro",
            },
        }));

        var service = new ScheduleRunCommandHandler(new ScheduleCommands(scheduler));
        var result = await service.ExecuteAsync(new ScheduleRunCliOptions(id.ToString()), CancellationToken.None);

        Assert.True(result.Success);
        await scheduler.Received(1).RunAsync(new TaskRequest(id, ExpectedScopeGeneration: 0), CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterLoad_DoesNotRunTask()
    {
        var id = Guid.NewGuid();
        var scheduler = CreateWorkflow();
        using var cts = new CancellationTokenSource();

        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await cts.CancelAsync();
            return new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
            {
            new()
            {
                Id = id,
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro",
            },
            });
        });

        var service = new ScheduleRunCommandHandler(new ScheduleCommands(scheduler));

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(new ScheduleRunCliOptions(id.ToString()), cts.Token));
        await scheduler.DidNotReceive().RunAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AddInterval_AddsAndSavesTask()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>()));
        var service = new ScheduleCommandHandler(new ScheduleCommands(scheduler));

        var result = await service.ExecuteAsync(
            new ScheduleCliOptions(
                ScheduleCliAction.Add,
                Name: "Daily",
                MacroFilePath: "/tmp/demo.macro",
                Interval: "10m",
                Enabled: true),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = Assert.IsType<ScheduleTaskData>(result.Data);
        Assert.Equal(10, taskData.IntervalValue);
        Assert.Equal("Minutes", taskData.IntervalUnit);
        _ = await scheduler.Received(1).AddAsync(Arg.Is<ScheduledTask>(task =>
            task != null
            && task.Name == "Daily"
            && task.MacroFilePath == "/tmp/demo.macro"
            && task.Type == ScheduleType.Interval
            && task.IntervalValue == 10
            && task.IntervalUnit == IntervalUnit.Minutes
            && task.IsEnabled),
            0, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_EditExistingTask_UpdatesAndSavesTask()
    {
        var id = new Guid(0x11111111, 0x1111, 0x1111, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
        var task = new ScheduledTask { Id = id, Name = "Old", MacroFilePath = "/tmp/old.macro" };
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask> { task }));
        var service = new ScheduleCommandHandler(new ScheduleCommands(scheduler));

        var result = await service.ExecuteAsync(
            new ScheduleCliOptions(
                ScheduleCliAction.Edit,
                TaskId: id.ToString(),
                Name: "New",
                Weekly: "mon,wed",
                Time: "09:30"),
            CancellationToken.None);

        Assert.True(result.Success);
        _ = await scheduler.Received(1).UpdateAsync(Arg.Is<ScheduledTask>(updated =>
            updated != null
            && updated.Id == id
            && updated.Name == "New"
            && updated.Type == ScheduleType.Weekly
            && updated.WeeklyDays == (ScheduleDays.Monday | ScheduleDays.Wednesday)
            && updated.WeeklyTime == new TimeSpan(9, 30, 0)),
            0, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RemoveMissingTask_ReturnsInvalidArguments()
    {
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>()));
        var service = new ScheduleCommandHandler(new ScheduleCommands(scheduler));

        var result = await service.ExecuteAsync(
            new ScheduleCliOptions(ScheduleCliAction.Remove, TaskId: "11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_EnableExistingTask_SavesMutation()
    {
        var id = new Guid(0x11111111, 0x1111, 0x1111, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new() { Id = id, Name = "Task", MacroFilePath = "/tmp/a.macro" },
        }));
        var service = new ScheduleCommandHandler(new ScheduleCommands(scheduler));

        var result = await service.ExecuteAsync(new ScheduleCliOptions(ScheduleCliAction.Enable, TaskId: id.ToString()), CancellationToken.None);

        Assert.True(result.Success);
        _ = await scheduler.Received(1).SetEnabledAsync(new TaskRequest(id, Enabled: true, ExpectedScopeGeneration: 0), CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Next_DoesNotSave()
    {
        var id = new Guid(0x11111111, 0x1111, 0x1111, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11);
        var scheduler = CreateWorkflow();
        _ = scheduler.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>(new ObservableCollection<ScheduledTask>
        {
            new() { Id = id, Name = "Task", MacroFilePath = "/tmp/a.macro", Type = ScheduleType.Interval, IntervalValue = 5, IntervalUnit = IntervalUnit.Minutes },
        }));
        var service = new ScheduleCommandHandler(new ScheduleCommands(scheduler));

        var result = await service.ExecuteAsync(new ScheduleCliOptions(ScheduleCliAction.Next, TaskId: id.ToString()), CancellationToken.None);

        Assert.True(result.Success);
    }
    private static IManageSchedule CreateWorkflow()
    {
        var workflow = Substitute.For<IManageSchedule>();
        _ = workflow.AddAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<ScheduledTask>());
        _ = workflow.UpdateAsync(Arg.Any<ScheduledTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<ScheduledTask>());
        return workflow;
    }

}
