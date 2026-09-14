namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class AutomationTaskCommitTests
{
    [Fact]
    public async Task WorkflowLists_DoNotReloadAndReplaceInitializedRuntimeTasks()
    {
        var repository = Substitute.For<IShortcutTaskRepository>();
        var persisted = new ShortcutTask { Name = "Persisted" };
        _ = repository.LoadAsync(Arg.Any<CancellationToken>()).Returns([ persisted ]);
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), shortcutsFilePath: "unused.json", taskRepository: repository);
        await service.LoadAsync();
        persisted.LastStatus = "Runtime progress";
        using var workflow = new ManageShortcut(service, service);

        var first = await workflow.ListAsync(cancellationToken: CancellationToken.None);
        var second = await workflow.ListAsync(cancellationToken: CancellationToken.None);

        Assert.Same(persisted, Assert.Single(service.Tasks));
        Assert.NotSame(persisted, Assert.Single(first.Tasks));
        Assert.Equal("Runtime progress", Assert.Single(second.Tasks).LastStatus);
        _ = await repository.Received(1).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortcutDebounce_UsesInjectedElapsedTime()
    {
        var clock = new FakeTimeProvider();
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), shortcutsFilePath: "unused.json", timeProvider: clock,
            taskRepository: Substitute.For<IShortcutTaskRepository>());
        service.AddTask(new ShortcutTask { Name = "Debounced", MacroFilePath = "/missing-" + Guid.NewGuid().ToString("N"), HotkeyString = "F5", IsEnabled = true });
        var executions = 0;
        service.ShortcutExecuted += (_, _) => executions++;
        var input = new RawHotkeyInputEventArgs(0, new HashSet<int>(), "F5");

        await service.HandleRawInputAsync(input);
        clock.Advance(TimeSpan.FromMilliseconds(299));
        await service.HandleRawInputAsync(input);
        Assert.Equal(1, executions);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await service.HandleRawInputAsync(input);
        Assert.Equal(2, executions);
    }

    [Fact]
    public async Task ShortcutCommit_PublishesOnlyAfterPersistenceAndKeepsExistingIdentity()
    {
        var repository = Substitute.For<IShortcutTaskRepository>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = repository.SaveAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            started.SetResult();
            await release.Task;
        });
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), shortcutsFilePath: "unused.json", taskRepository: repository);
        var live = new ShortcutTask { Name = "Original" };
        service.AddTask(live);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Edited";
        var replacements = 0;
        service.Tasks.CollectionChanged += (_, change) =>
        {
            if (change.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                replacements++;
            }
        };

        var commit = service.CommitAsync([draft], cancellationToken: CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
        try
        {
            Assert.Equal("Original", live.Name);
            Assert.Equal(0, replacements);
            live.LastStatus = "Completed while saving";
            live.LastTriggeredTime = DateTime.UtcNow;
        }
        finally
        {
            release.SetResult();
        }
        await commit;

        Assert.Same(live, Assert.Single(service.Tasks));
        Assert.Equal("Edited", live.Name);
        Assert.Equal(1, replacements);
        Assert.Equal("Completed while saving", live.LastStatus);
        _ = Assert.NotNull(live.LastTriggeredTime);
    }

    [Fact]
    public async Task TriggerCommit_FailedWriteLeavesMembershipAndValuesUnchanged()
    {
        var repository = Substitute.For<ITriggerTaskRepository>();
        _ = repository.SaveAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Disk failed")));
        using var service = new TriggerService(windowManager: null, Substitute.For<IProfileSwitchRequests>(), Substitute.For<IMacroFileManager>(),
            () => Substitute.For<IMacroPlayer>(), "unused.json", TimeProvider.System, repository);
        var live = new TriggerTask { Name = "Original" };
        service.AddTask(live);

        _ = await Assert.ThrowsAsync<IOException>(() => service.CommitAsync([], cancellationToken: CancellationToken.None));

        Assert.Same(live, Assert.Single(service.Tasks));
        Assert.Equal("Original", live.Name);
    }

    [Fact]
    public async Task ScheduleCommit_PreservesRuntimeProgressAndAppliesPersistedOrder()
    {
        var clock = new FakeTimeProvider();
        var repository = Substitute.For<IScheduledTaskRepository>();
        using var service = new SchedulerService(repository, Substitute.For<IScheduledTaskExecutor>(), clock);
        var first = new ScheduledTask { Name = "First", MacroFilePath = "first.macro", IsEnabled = true };
        var second = new ScheduledTask { Name = "Second", MacroFilePath = "second.macro", IsEnabled = true };
        service.AddTask(first);
        service.AddTask(second);
        var next = clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        first.NextRunTime = next;
        var firstDraft = AutomationTaskSnapshots.Copy(first);
        firstDraft.Name = "Edited";
        _ = repository.SaveAsync(Arg.Any<IEnumerable<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            first.LastStatus = "Finished during write";
            first.NextRunTime = next.AddMinutes(5);
            return Task.CompletedTask;
        });

        await service.CommitAsync([AutomationTaskSnapshots.Copy(second), firstDraft], cancellationToken: CancellationToken.None);

        Assert.Same(second, service.Tasks[0]);
        Assert.Same(first, service.Tasks[1]);
        Assert.Equal("Edited", first.Name);
        Assert.Equal("Finished during write", first.LastStatus);
        Assert.Equal(next.AddMinutes(5), first.NextRunTime);
    }

    [Fact]
    public async Task ScheduleCommit_CancellationAfterTheWriteStillPublishesCommittedState()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = Substitute.For<IScheduledTaskRepository>();
        _ = repository.SaveAsync(Arg.Any<IEnumerable<ScheduledTask>>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await cancellation.CancelAsync();
        });
        using var service = new SchedulerService(repository, Substitute.For<IScheduledTaskExecutor>(), TimeProvider.System);
        var live = new ScheduledTask { Name = "Original" };
        service.AddTask(live);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.Name = "Committed";

        await service.CommitAsync([draft], cancellation.Token);

        Assert.Same(live, Assert.Single(service.Tasks));
        Assert.Equal("Committed", live.Name);
    }
}
