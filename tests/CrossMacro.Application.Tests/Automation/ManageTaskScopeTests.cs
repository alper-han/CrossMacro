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
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns(_ => [ current ]);
        var operations = Substitute.For<IScheduledTaskOperations>();
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new ScheduledTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: TestContext.Current.CancellationToken);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new ScheduledTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            4 => () => workflow.RunAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns(_ => [ current ]);
        var operations = Substitute.For<IShortcutTaskOperations>();
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new ShortcutTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: TestContext.Current.CancellationToken);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new ShortcutTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            4 => () => workflow.RunAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());
        await operations.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
        var store = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        _ = store.Tasks.Returns(_ => [ current ]);
        using var workflow = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, store, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var draft = Assert.Single(snapshot.Tasks);
        draft.Name = "Old draft edit";
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            current = new TriggerTask { Id = original.Id, Name = "Current profile" };
            return Task.CompletedTask;
        }, cancellationToken: TestContext.Current.CancellationToken);
        store.ClearReceivedCalls();

        Func<Task> mutation = operation switch
        {
            0 => () => workflow.AddAsync(new TriggerTask { Name = "Old add dialog" }, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            1 => () => workflow.UpdateAsync(draft, snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken),
            2 => () => workflow.RemoveAsync(new TaskRequest(draft.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            3 => () => workflow.SetEnabledAsync(new TaskRequest(draft.Id, Enabled: true, snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var conflict = await Assert.ThrowsAsync<TaskScopeConflictException>(mutation);

        Assert.Equal(TaskScopeConflictException.ConflictMessage, conflict.Message);
        Assert.Equal("Current profile", current.Name);
        await store.DidNotReceive().LoadAsync();
        await store.DidNotReceive().CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());
        var refreshed = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(snapshot.ScopeGeneration + 1, refreshed.ScopeGeneration);
        Assert.Equal("Current profile", Assert.Single(refreshed.Tasks).Name);
    }

    [Fact]
    public async Task ScopeValidation_HappensAfterWaitingForProfilePublication()
    {
        using var gate = new AutomationTaskMutationGate();
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns([]);
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, Substitute.For<IScheduledTaskOperations>().RunTaskAsync, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var profile = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            _ = gate.AdvanceScopeGeneration();
        }, cancellationToken: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken);
        var pending = workflow.AddAsync(new ScheduledTask(), snapshot.ScopeGeneration, cancellationToken: TestContext.Current.CancellationToken);
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
        var store = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        _ = store.Tasks.Returns([ task ]);
        var operations = Substitute.For<IScheduledTaskOperations>();
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admitted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = operations.RunTaskAsync(task.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            admitted.SetResult(gate.ScopeGeneration);
            return execution.Task;
        });
        using var workflow = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, store, operations.RunTaskAsync, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var running = workflow.RunAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(snapshot.ScopeGeneration, await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(running.IsCompleted);
            await gate.RunAsync(() =>
            {
                _ = gate.AdvanceScopeGeneration();
                return Task.CompletedTask;
            }, cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken);
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
        var store = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        _ = store.Tasks.Returns([ task ]);
        var operations = Substitute.For<IShortcutTaskOperations>();
        var execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admitted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = operations.RunTaskAsync(task.Id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            admitted.SetResult(gate.ScopeGeneration);
            return execution.Task;
        });
        using var workflow = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, operations.RunTaskAsync, gate);
        var snapshot = await workflow.ListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var running = workflow.RunAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: snapshot.ScopeGeneration), cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(snapshot.ScopeGeneration, await admitted.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(running.IsCompleted);
            await gate.RunAsync(() =>
            {
                _ = gate.AdvanceScopeGeneration();
                return Task.CompletedTask;
            }, cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: TestContext.Current.CancellationToken);
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
        var scheduleStore = Substitute.For<IAutomationTaskStore<ScheduledTask>>();
        var shortcutStore = Substitute.For<IAutomationTaskStore<ShortcutTask>>();
        var triggerStore = Substitute.For<IAutomationTaskStore<TriggerTask>>();
        using var schedules = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, scheduleStore, Substitute.For<IScheduledTaskOperations>().RunTaskAsync, gate);
        using var shortcuts = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, shortcutStore, Substitute.For<IShortcutTaskOperations>().RunTaskAsync, gate);
        using var triggers = new ManageAutomationTasks<TriggerTask>(TriggerTaskKind.Instance, triggerStore, gate);
        await gate.RunAsync(() =>
        {
            gate.MarkFaulted();
            return Task.CompletedTask;
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(gate.IsFaulted);
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => schedules.ListAsync(cancellationToken: TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => shortcuts.ListAsync(cancellationToken: TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => triggers.ListAsync(cancellationToken: TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => schedules.AddAsync(new ScheduledTask(), cancellationToken: TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => shortcuts.AddAsync(new ShortcutTask(), cancellationToken: TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => triggers.AddAsync(new TriggerTask(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(scheduleStore.ReceivedCalls());
        Assert.Empty(shortcutStore.ReceivedCalls());
        Assert.Empty(triggerStore.ReceivedCalls());
    }
}
