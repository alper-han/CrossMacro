using System.Collections.ObjectModel;

namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskSnapshotTests
{
    [Fact]
    public async Task ManageShortcut_ListReturnsStableReadOnlySnapshot()
    {
        var first = new ShortcutTask { Id = Guid.NewGuid() };
        var second = new ShortcutTask { Id = Guid.NewGuid() };
        var tasks = new ObservableCollection<ShortcutTask> { first };
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync).ListAsync(TestContext.Current.CancellationToken);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.NotSame(first, result.Tasks[0]);
        Assert.Equal(first.Id, result.Tasks[0].Id);
        var readOnly = Assert.IsAssignableFrom<IList<ShortcutTask>>(result.Tasks);
        Assert.True(readOnly.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => readOnly.Add(second));
    }

    [Fact]
    public async Task ManageSchedule_ListReturnsStableReadOnlySnapshot()
    {
        var first = new ScheduledTask { Id = Guid.NewGuid() };
        var second = new ScheduledTask { Id = Guid.NewGuid() };
        var tasks = new ObservableCollection<ScheduledTask> { first };
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync).ListAsync(TestContext.Current.CancellationToken);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.NotSame(first, result.Tasks[0]);
        Assert.Equal(first.Id, result.Tasks[0].Id);
        var readOnly = Assert.IsAssignableFrom<IList<ScheduledTask>>(result.Tasks);
        Assert.True(readOnly.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => readOnly.Add(second));
    }

    [Fact]
    public async Task ManageTrigger_ListReturnsStableReadOnlySnapshot()
    {
        var first = new TriggerTask { Id = Guid.NewGuid() };
        var second = new TriggerTask { Id = Guid.NewGuid() };
        var tasks = new ObservableCollection<TriggerTask> { first };
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store).ListAsync(TestContext.Current.CancellationToken);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.NotSame(first, result.Tasks[0]);
        Assert.Equal(first.Id, result.Tasks[0].Id);
        var readOnly = Assert.IsAssignableFrom<IList<TriggerTask>>(result.Tasks);
        Assert.True(readOnly.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => readOnly.Add(second));
    }
}
