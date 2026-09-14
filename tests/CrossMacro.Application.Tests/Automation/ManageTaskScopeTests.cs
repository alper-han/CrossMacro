namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTaskScopeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ScheduleMutation_FromReplacedProfile_IsRejectedBeforeStoreAccess(int operation)
    {
        using var gate = new AutomationTaskMutationGate();
        var original = new ScheduledTask { Name = "Original profile" };
        var current = original;
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns(_ => [ current ]);
        var operations = Substitute.For<IScheduledTaskOperations>();
        using var workflow = new ManageSchedule(operations, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new ScheduledTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new ScheduledTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            4 => () => workflow.RunAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        Assert.Equal(snapshot.ScopeGeneration + 1, refreshed.ScopeGeneration);
        Assert.Equal("Current profile", Assert.Single(refreshed.Tasks).Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ShortcutMutation_FromReplacedProfile_IsRejectedBeforeStoreAccess(int operation)
    {
        using var gate = new AutomationTaskMutationGate();
        var original = new ShortcutTask { Name = "Original profile" };
        var current = original;
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns(_ => [ current ]);
        var operations = Substitute.For<IShortcutTaskOperations>();
        using var workflow = new ManageShortcut(operations, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new ShortcutTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new ShortcutTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            4 => () => workflow.RunAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        Assert.Equal(snapshot.ScopeGeneration + 1, refreshed.ScopeGeneration);
        Assert.Equal("Current profile", Assert.Single(refreshed.Tasks).Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TriggerMutation_FromReplacedProfile_IsRejectedBeforeStoreAccess(int operation)
    {
        using var gate = new AutomationTaskMutationGate();
        var original = new TriggerTask { Name = "Original profile" };
        var current = original;
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns(_ => [ current ]);
        var operations = Substitute.For<ITriggerTaskOperations>();
        using var workflow = new ManageTrigger(operations, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new TriggerTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new TriggerTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: CancellationToken.None),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        Assert.Equal(snapshot.ScopeGeneration + 1, refreshed.ScopeGeneration);
        Assert.Equal("Current profile", Assert.Single(refreshed.Tasks).Name);
    }

    [Fact]
    public async Task ScopeValidation_HappensAfterWaitingForProfilePublication()
    {
        using var gate = new AutomationTaskMutationGate();
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([]);
        using var workflow = new ManageSchedule(Substitute.For<IScheduledTaskOperations>(), store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var profile = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            _ = gate.AdvanceScopeGeneration();
        }, cancellationToken: CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
        var pending = workflow.AddAsync(new ScheduledTask(), snapshot.ScopeGeneration, cancellationToken: CancellationToken.None);
        Assert.False(pending.IsCompleted);
        release.SetResult();
        await profile;

        _ = await Assert.ThrowsAsync<TaskScopeConflictException>(() => pending);
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleRun_BindsUnderScopeGate_AndReleasesItBeforeExecutionCompletes()
    {
        using var gate = new AutomationTaskMutationGate();
        var task = new ScheduledTask();
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([ task ]);
        var operations = Substitute.For<IScheduledTaskOperations>();
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admitted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = operations.RunTaskAsync(task.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            admitted.SetResult(gate.ScopeGeneration);
            return execution.Task;
        });
        using var workflow = new ManageSchedule(operations, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var running = workflow.RunAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None);
        try
        {
            Assert.Equal(snapshot.ScopeGeneration, await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None));
            Assert.False(running.IsCompleted);
            await gate.RunAsync(() =>
            {
                _ = gate.AdvanceScopeGeneration();
                return Task.CompletedTask;
            }, cancellationToken: CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
            Assert.Equal(snapshot.ScopeGeneration + 1, gate.ScopeGeneration);
        }
        finally
        {
            _ = execution.TrySetResult();
            await running;
        }
    }

    [Fact]
    public async Task ShortcutRun_BindsUnderScopeGate_AndReleasesItBeforeExecutionCompletes()
    {
        using var gate = new AutomationTaskMutationGate();
        var task = new ShortcutTask();
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([ task ]);
        var operations = Substitute.For<IShortcutTaskOperations>();
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admitted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = operations.RunTaskAsync(task.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            admitted.SetResult(gate.ScopeGeneration);
            return execution.Task;
        });
        using var workflow = new ManageShortcut(operations, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var running = workflow.RunAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: CancellationToken.None);
        try
        {
            Assert.Equal(snapshot.ScopeGeneration, await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None));
            Assert.False(running.IsCompleted);
            await gate.RunAsync(() =>
            {
                _ = gate.AdvanceScopeGeneration();
                return Task.CompletedTask;
            }, cancellationToken: CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
            Assert.Equal(snapshot.ScopeGeneration + 1, gate.ScopeGeneration);
        }
        finally
        {
            _ = execution.TrySetResult();
            await running;
        }
    }

    [Fact]
    public async Task FaultedProfileGate_RejectsListsAndUnscopedMutationsBeforeStoreAccess()
    {
        using var gate = new AutomationTaskMutationGate();
        var scheduleStore = Substitute.For<IScheduledTaskStore>();
        var shortcutStore = Substitute.For<IShortcutTaskStore>();
        var triggerStore = Substitute.For<ITriggerTaskStore>();
        using var schedules = new ManageSchedule(Substitute.For<IScheduledTaskOperations>(), scheduleStore, gate);
        using var shortcuts = new ManageShortcut(Substitute.For<IShortcutTaskOperations>(), shortcutStore, gate);
        using var triggers = new ManageTrigger(Substitute.For<ITriggerTaskOperations>(), triggerStore, gate);
        await gate.RunAsync(() =>
        {
            gate.MarkFaulted();
            return Task.CompletedTask;
        }, cancellationToken: CancellationToken.None);

        Assert.True(gate.IsFaulted);
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => schedules.ListAsync(cancellationToken: CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => shortcuts.ListAsync(cancellationToken: CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => triggers.ListAsync(cancellationToken: CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => schedules.AddAsync(new ScheduledTask(), cancellationToken: CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => shortcuts.AddAsync(new ShortcutTask(), cancellationToken: CancellationToken.None));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => triggers.AddAsync(new TriggerTask(), cancellationToken: CancellationToken.None));
        Assert.Empty(scheduleStore.ReceivedCalls());
        Assert.Empty(shortcutStore.ReceivedCalls());
        Assert.Empty(triggerStore.ReceivedCalls());
    }
}
