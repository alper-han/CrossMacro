namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskCommitTests
{
    [Fact]
    public async Task TaskCommandResult_ContainsTheNormalizedCommittedValues()
    {
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns([]);
        ScheduledTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<ScheduledTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, Substitute.For<IScheduledTaskOperations>().RunTaskAsync);
        var commands = TaskCommandTestFactory.Schedule(workflow);

        var result = await commands.ExecuteAsync(new ScheduleCommand(TaskCommandAction.Add, Name: "Normalized", MacroFilePath: "macro", Interval: "5s", Speed: -1), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(committed);
        Assert.NotNull(result.Task);
        Assert.Equal(committed.PlaybackSpeed, result.Task.PlaybackSpeed);
        Assert.Equal(PlaybackOptions.MinSpeedMultiplier, result.Task.PlaybackSpeed);
    }

    [Fact]
    public async Task ShortcutCommit_NormalizesTheDetachedDraftBeforePersistence()
    {
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns([]);
        ShortcutTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<ShortcutTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, Substitute.For<IShortcutTaskOperations>().RunTaskAsync);

        var result = await workflow.AddAsync(new ShortcutTask
        {
            PlaybackSpeed = -1,
            LoopEnabled = true,
            RunWhileHeld = true,
            IsEnabled = true,
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(committed);
        Assert.Same(committed, result);
        Assert.Equal(PlaybackOptions.MinSpeedMultiplier, committed.PlaybackSpeed);
        Assert.True(committed.LoopEnabled);
        Assert.False(committed.RunWhileHeld);
        Assert.False(committed.IsEnabled);
    }

    [Fact]
    public async Task TriggerCommit_RejectsInvalidEnablementBeforePersistence()
    {
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        _ = store.Tasks.Returns([]);
        TriggerTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<TriggerTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store);

        var result = await workflow.AddAsync(new TriggerTask
        {
            Action = TriggerOperation.RunMacro,
            MacroFilePath = string.Empty,
            IsEnabled = true,
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(committed);
        Assert.Same(committed, result);
        Assert.False(committed.IsEnabled);
    }

    [Fact]
    public async Task ProfileReplacement_WaitsUntilTheTaskMutationHasPublished()
    {
        using var gate = new AutomationTaskMutationGate();
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns([]);
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            persisted.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        });
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, Substitute.For<IShortcutTaskOperations>().RunTaskAsync, gate);
        var mutation = workflow.AddAsync(new ShortcutTask { Name = "Old profile task" }, cancellationToken: TestContext.Current.CancellationToken);
        await persisted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken);
        var profileReplaced = false;
        var replacement = gate.RunAsync(() =>
        {
            profileReplaced = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
        try
        {
            Assert.False(profileReplaced);
        }
        finally
        {
            release.SetResult();
        }

        _ = await mutation;
        await replacement;
        Assert.True(profileReplaced);
    }

    [Fact]
    public async Task ScheduleUpdate_WhenPersistenceFails_DoesNotMutateTheLiveTask()
    {
        var live = new ScheduledTask { Name = "Original" };
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns([live]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ScheduleUpdate_PersistsDetachedValues()
    {
        var live = new ScheduledTask { Name = "Original" };
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns([live]);
        IReadOnlyList<ScheduledTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<ScheduledTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

    [Fact]
    public async Task ShortcutUpdate_WhenPersistenceFails_DoesNotMutateTheLiveTask()
    {
        var live = new ShortcutTask { Name = "Original" };
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns([live]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ShortcutUpdate_PersistsDetachedValues()
    {
        var live = new ShortcutTask { Name = "Original" };
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns([live]);
        IReadOnlyList<ShortcutTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<ShortcutTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

    [Fact]
    public async Task TriggerUpdate_WhenPersistenceFails_DoesNotMutateTheLiveTask()
    {
        var live = new TriggerTask { Name = "Original" };
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        _ = store.Tasks.Returns([live]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task TriggerUpdate_PersistsDetachedValues()
    {
        var live = new TriggerTask { Name = "Original" };
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        _ = store.Tasks.Returns([live]);
        IReadOnlyList<TriggerTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<TriggerTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

}
