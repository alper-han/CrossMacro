
namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskWorkflowCancellationTests
{
    [Fact]
    public async Task ManageShortcut_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IShortcutTaskStore>();
        var task = new ShortcutTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<ShortcutTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageShortcut(operations, store);

        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        operations.DidNotReceive().AddTask(Arg.Any<ShortcutTask>());
        operations.DidNotReceive().UpdateTask(Arg.Any<ShortcutTask>());
        operations.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        operations.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageSchedule_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IScheduledTaskStore>();
        var task = new ScheduledTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<ScheduledTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageSchedule(operations, store);

        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        operations.DidNotReceive().AddTask(Arg.Any<ScheduledTask>());
        operations.DidNotReceive().UpdateTask(Arg.Any<ScheduledTask>());
        operations.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        operations.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageTrigger_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var operations = Substitute.For<ITriggerTaskOperations>();
        var store = Substitute.For<ITriggerTaskStore>();
        var task = new TriggerTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageTrigger(operations, store);

        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        operations.DidNotReceive().AddTask(Arg.Any<TriggerTask>());
        operations.DidNotReceive().UpdateTask(Arg.Any<TriggerTask>());
        operations.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        operations.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await store.DidNotReceive().SaveAsync();
    }
}
