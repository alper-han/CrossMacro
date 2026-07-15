
namespace CrossMacro.Cli.Tests;

public sealed class ManageTaskWorkflowCancellationTests
{
    [Fact]
    public async Task ManageShortcut_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var service = Substitute.For<IShortcutService>();
        var task = new ShortcutTask { Id = Guid.NewGuid() };
        service.Tasks.Returns(new ObservableCollection<ShortcutTask> { task });
        CancellationTokenSource? currentCancellation = null;
        service.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageShortcut(service);

        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        service.DidNotReceive().AddTask(Arg.Any<ShortcutTask>());
        service.DidNotReceive().UpdateTask(Arg.Any<ShortcutTask>());
        service.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        service.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await service.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageSchedule_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var service = Substitute.For<ISchedulerService>();
        var task = new ScheduledTask { Id = Guid.NewGuid() };
        service.Tasks.Returns(new ObservableCollection<ScheduledTask> { task });
        CancellationTokenSource? currentCancellation = null;
        service.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageSchedule(service);

        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        service.DidNotReceive().AddTask(Arg.Any<ScheduledTask>());
        service.DidNotReceive().UpdateTask(Arg.Any<ScheduledTask>());
        service.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        service.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await service.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageTrigger_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var service = Substitute.For<ITriggerService>();
        var task = new TriggerTask { Id = Guid.NewGuid() };
        service.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        CancellationTokenSource? currentCancellation = null;
        service.LoadAsync().Returns(_ =>
        {
            currentCancellation!.Cancel();
            return Task.CompletedTask;
        });
        var workflow = new ManageTrigger(service);

        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.AddAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.UpdateAsync(task, currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.RemoveAsync(new TaskRequest(task.Id), currentCancellation.Token));
        currentCancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), currentCancellation.Token));

        service.DidNotReceive().AddTask(Arg.Any<TriggerTask>());
        service.DidNotReceive().UpdateTask(Arg.Any<TriggerTask>());
        service.DidNotReceive().RemoveTask(Arg.Any<Guid>());
        service.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await service.DidNotReceive().SaveAsync();
    }
}
