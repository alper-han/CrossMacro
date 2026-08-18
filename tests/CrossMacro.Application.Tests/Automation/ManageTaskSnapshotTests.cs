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
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageShortcut(operations, store).ListAsync(CancellationToken.None);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.Same(first, result.Tasks[0]);
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
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageSchedule(operations, store).ListAsync(CancellationToken.None);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.Same(first, result.Tasks[0]);
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
        var operations = Substitute.For<ITriggerTaskOperations>();
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns(tasks);
        _ = store.LoadAsync().Returns(Task.CompletedTask);

        var result = await new ManageTrigger(operations, store).ListAsync(CancellationToken.None);

        tasks.Add(second);

        _ = Assert.Single(result.Tasks);
        Assert.Same(first, result.Tasks[0]);
        var readOnly = Assert.IsAssignableFrom<IList<TriggerTask>>(result.Tasks);
        Assert.True(readOnly.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => readOnly.Add(second));
    }
}
