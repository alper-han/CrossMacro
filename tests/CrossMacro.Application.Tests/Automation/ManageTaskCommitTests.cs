namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskCommitTests
{
    [Fact]
    public async Task TaskCommandResult_ContainsTheNormalizedCommittedValues()
    {
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([]);
        ScheduledTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<ScheduledTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageSchedule(Substitute.For<IScheduledTaskOperations>(), store);
        var commands = new ScheduleCommands(workflow);

        var result = await commands.ExecuteAsync(new ScheduleCommand(ScheduleCommandAction.Add, Name: "Normalized", MacroFilePath: "macro", Interval: "5s", Speed: -1), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(committed);
        Assert.NotNull(result.Task);
        Assert.Equal(committed.PlaybackSpeed, result.Task.PlaybackSpeed);
        Assert.Equal(PlaybackOptions.MinSpeedMultiplier, result.Task.PlaybackSpeed);
    }

    [Fact]
    public async Task ShortcutCommit_NormalizesTheDetachedDraftBeforePersistence()
    {
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([]);
        ShortcutTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<ShortcutTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageShortcut(Substitute.For<IShortcutTaskOperations>(), store);

        var result = await workflow.AddAsync(new ShortcutTask
        {
            PlaybackSpeed = -1,
            LoopEnabled = true,
            RunWhileHeld = true,
            IsEnabled = true,
        }, cancellationToken: CancellationToken.None);

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
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns([]);
        TriggerTask? committed = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            committed = Assert.Single(call.Arg<IReadOnlyList<TriggerTask>>());
            return Task.CompletedTask;
        });
        using var workflow = new ManageTrigger(Substitute.For<ITriggerTaskOperations>(), store);

        var result = await workflow.AddAsync(new TriggerTask
        {
            Action = TriggerOperation.RunMacro,
            MacroFilePath = string.Empty,
            IsEnabled = true,
        }, cancellationToken: CancellationToken.None);

        Assert.NotNull(committed);
        Assert.Same(committed, result);
        Assert.False(committed.IsEnabled);
    }

    [Fact]
    public async Task ProfileReplacement_WaitsUntilTheTaskMutationHasPublished()
    {
        using var gate = new AutomationTaskMutationGate();
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([]);
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            persisted.SetResult();
            await release.Task;
        });
        using var workflow = new ManageShortcut(Substitute.For<IShortcutTaskOperations>(), store, gate);
        var mutation = workflow.AddAsync(new ShortcutTask { Name = "Old profile task" }, cancellationToken: CancellationToken.None);
        await persisted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
        var profileReplaced = false;
        var replacement = gate.RunAsync(() =>
        {
            profileReplaced = true;
            return Task.CompletedTask;
        }, CancellationToken.None);
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
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageSchedule(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        operations.DidNotReceive().UpdateTask(Arg.Any<ScheduledTask>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ScheduleUpdate_PersistsDetachedValues()
    {
        var live = new ScheduledTask { Name = "Original" };
        var operations = Substitute.For<IScheduledTaskOperations>();
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        IReadOnlyList<ScheduledTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<ScheduledTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageSchedule(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

    [Fact]
    public async Task ShortcutUpdate_WhenPersistenceFails_DoesNotMutateTheLiveTask()
    {
        var live = new ShortcutTask { Name = "Original" };
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageShortcut(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        operations.DidNotReceive().UpdateTask(Arg.Any<ShortcutTask>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ShortcutUpdate_PersistsDetachedValues()
    {
        var live = new ShortcutTask { Name = "Original" };
        var operations = Substitute.For<IShortcutTaskOperations>();
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        IReadOnlyList<ShortcutTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<ShortcutTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageShortcut(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

    [Fact]
    public async Task TriggerUpdate_WhenPersistenceFails_DoesNotMutateTheLiveTask()
    {
        var live = new TriggerTask { Name = "Original" };
        var operations = Substitute.For<ITriggerTaskOperations>();
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Persistence failed")));
        using var workflow = new ManageTrigger(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        _ = await Assert.ThrowsAsync<IOException>(() => workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None));

        Assert.Equal("Original", live.Name);
        Assert.Equal("Edited", draft.Name);
        operations.DidNotReceive().UpdateTask(Arg.Any<TriggerTask>());
        await store.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task TriggerUpdate_PersistsDetachedValues()
    {
        var live = new TriggerTask { Name = "Original" };
        var operations = Substitute.For<ITriggerTaskOperations>();
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns([ live ]);
        IReadOnlyList<TriggerTask>? persisted = null;
        _ = store.CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persisted = call.ArgAt<IReadOnlyList<TriggerTask>>(0);
            Assert.Equal("Original", live.Name);
            return Task.CompletedTask;
        });
        using var workflow = new ManageTrigger(operations, store);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";

        var result = await workflow.UpdateAsync(draft, cancellationToken: CancellationToken.None);

        Assert.Equal("Edited", result.Name);
        Assert.NotSame(draft, Assert.Single(persisted!));
        Assert.Equal("Original", live.Name);
    }

}
