namespace CrossMacro.Application.Tests.Automation;

public sealed class TaskCommandsTests
{
    [Fact]
    public async Task Remove_RemovesTheFoundTaskUnderTheListedScope()
    {
        var existing = new TriggerTask { Name = "Existing", TargetProfileId = "dev" };
        var workflow = Substitute.For<IManageAutomationTasks<TriggerTask>>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>([existing], 7));

        var result = await TaskCommandTestFactory.Trigger(workflow).ExecuteAsync(
            new TriggerCommand(TaskCommandAction.Remove, TaskId: existing.Id.ToString()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Trigger task removed: Existing.", result.Message);
        _ = await workflow.Received(1).RemoveAsync(
            Arg.Is<TaskRequest>(request => request.Id == existing.Id && request.ExpectedScopeGeneration == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_WithAnInvalidId_FailsWithoutCallingTheWorkflow()
    {
        var workflow = Substitute.For<IManageRunnableAutomationTasks<ScheduledTask>>();

        var result = await TaskCommandTestFactory.Schedule(workflow).ExecuteAsync(
            new ScheduleCommand(TaskCommandAction.Remove, TaskId: "not-a-guid"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Invalid schedule task id format.", result.Message);
        _ = await workflow.DidNotReceive().RemoveAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_OfAnUnknownTask_ReportsNotFound()
    {
        var workflow = Substitute.For<IManageRunnableAutomationTasks<ShortcutTask>>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([], 1));

        var result = await TaskCommandTestFactory.Shortcut(workflow).ExecuteAsync(
            new ShortcutCommand(TaskCommandAction.Remove, TaskId: Guid.NewGuid().ToString()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Shortcut task not found.", result.Message);
    }

    [Fact]
    public async Task Enable_OfATaskThatCannotBeEnabled_FailsWithItsReason()
    {
        var existing = new TriggerTask { Name = "Incomplete", Field = TriggerField.WindowTitle, Value = string.Empty };
        var workflow = Substitute.For<IManageAutomationTasks<TriggerTask>>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>([existing], 1));

        var result = await TaskCommandTestFactory.Trigger(workflow).ExecuteAsync(
            new TriggerCommand(TaskCommandAction.Enable, TaskId: existing.Id.ToString()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Trigger task cannot be enabled.", result.Message);
        _ = await workflow.DidNotReceive().SetEnabledAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WhenTheScopeIsUnavailable_ReportsAFailedResult()
    {
        var workflow = Substitute.For<IManageRunnableAutomationTasks<ScheduledTask>>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<TaskCollectionResult<ScheduledTask>>(new TaskScopeUnavailableException()));

        var result = await TaskCommandTestFactory.Schedule(workflow).ListAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(new TaskScopeUnavailableException().Message, result.Message);
    }

    [Fact]
    public async Task List_ReportsTheTaskCount()
    {
        var workflow = Substitute.For<IManageRunnableAutomationTasks<ScheduledTask>>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ScheduledTask>([new ScheduledTask(), new ScheduledTask()], 1));

        var result = await TaskCommandTestFactory.Schedule(workflow).ListAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Loaded 2 schedule task(s).", result.Message);
        Assert.Equal(2, result.Tasks!.Count);
    }
}
