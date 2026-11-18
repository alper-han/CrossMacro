using System.Collections.ObjectModel;

namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskWorkflowCancellationTests
{
    [Fact]
    public async Task ManageShortcut_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        var task = new ShortcutTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<ShortcutTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(async _ =>
        {
            await (currentCancellation ?? throw new InvalidOperationException("Cancellation source was not initialized.")).CancelAsync();
        });
        var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync);

        async Task AssertLoadCancellationAsync(Func<CancellationToken, Task> operation)
        {
            using var cancellation = new CancellationTokenSource();
            currentCancellation = cancellation;
            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation(cancellation.Token));
        }

        await AssertLoadCancellationAsync(token => workflow.AddAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.UpdateAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.RemoveAsync(new TaskRequest(task.Id), token));
        await AssertLoadCancellationAsync(token => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), token));

        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageSchedule_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        var task = new ScheduledTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<ScheduledTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(async _ =>
        {
            await (currentCancellation ?? throw new InvalidOperationException("Cancellation source was not initialized.")).CancelAsync();
        });
        var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync);

        async Task AssertLoadCancellationAsync(Func<CancellationToken, Task> operation)
        {
            using var cancellation = new CancellationTokenSource();
            currentCancellation = cancellation;
            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation(cancellation.Token));
        }

        await AssertLoadCancellationAsync(token => workflow.AddAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.UpdateAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.RemoveAsync(new TaskRequest(task.Id), token));
        await AssertLoadCancellationAsync(token => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), token));

        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManageTrigger_WhenLoadCancels_DoesNotMutateOrSave()
    {
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        var task = new TriggerTask { Id = Guid.NewGuid() };
        _ = store.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        CancellationTokenSource? currentCancellation = null;
        _ = store.LoadAsync().Returns(async _ =>
        {
            await (currentCancellation ?? throw new InvalidOperationException("Cancellation source was not initialized.")).CancelAsync();
        });
        var workflow = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store);

        async Task AssertLoadCancellationAsync(Func<CancellationToken, Task> operation)
        {
            using var cancellation = new CancellationTokenSource();
            currentCancellation = cancellation;
            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation(cancellation.Token));
        }

        await AssertLoadCancellationAsync(token => workflow.AddAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.UpdateAsync(task, token));
        await AssertLoadCancellationAsync(token => workflow.RemoveAsync(new TaskRequest(task.Id), token));
        await AssertLoadCancellationAsync(token => workflow.SetEnabledAsync(new TaskRequest(task.Id, Enabled: true), token));

        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync();
    }
}
