namespace CrossMacro.Infrastructure.Tests.Services.Profiles;

public sealed class ProfileRuntimeCoordinatorTests : IDisposable
{
    private readonly TempConfigDirectory _config = new("CrossMacroProfileRuntimeTests_");

    public void Dispose()
    {
        _config.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotentAfterRuntimeServicesAreLoaded()
    {
        using var fixture = new ProfileCoordinatorFixture(_config.Path, withScheduler: true);

        await fixture.Coordinator.InitializeAsync();
        await fixture.Coordinator.InitializeAsync();

        _ = fixture.Coordinator.IsInitialized.Should().BeTrue();
        await fixture.Settings.Received(1).ReloadAsync(Arg.Any<string>());
        await fixture.Hotkeys.Received(1).ReloadAsync(Arg.Any<string>());
        await fixture.Scheduler!.Received(1).LoadAsync();
        await fixture.ScheduledTasks.Received(1).ReloadAsync(Arg.Any<string>());
        await fixture.TextExpansions.Received(1).ReloadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task InitializeAsync_WaitsForAutomationMutationBeforeReloadingProfile()
    {
        using var mutations = new AutomationTaskMutationGate();
        using var fixture = new ProfileCoordinatorFixture(_config.Path, mutations: mutations);
        var releaseMutation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mutation = mutations.RunAsync(() => releaseMutation.Task, TestContext.Current.CancellationToken);

        var initialization = fixture.Coordinator.InitializeAsync();

        _ = initialization.IsCompleted.Should().BeFalse();
        _ = fixture.Settings.ReceivedCalls().Should().BeEmpty();
        releaseMutation.SetResult();
        await mutation;
        await initialization;
        Assert.Equal(1, mutations.ScopeGeneration);
        await fixture.Coordinator.InitializeAsync();
        Assert.Equal(1, mutations.ScopeGeneration);
        _ = fixture.Coordinator.IsInitialized.Should().BeTrue();
        await fixture.Settings.Received(1).ReloadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SwitchProfileAsync_PublishesChangedEventAfterReleasingAutomationMutationGate()
    {
        using var mutations = new AutomationTaskMutationGate();
        using var fixture = new ProfileCoordinatorFixture(_config.Path, mutations: mutations);
        var manager = fixture.Coordinator;
        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Other");
        Task? reentrantMutation = null;
        manager.ProfileChanged += (_, _) =>
        {
            reentrantMutation = mutations.RunAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken);
            _ = reentrantMutation.IsCompletedSuccessfully.Should().BeTrue();
        };

        await manager.SwitchProfileAsync(profile.Id);

        _ = reentrantMutation.Should().NotBeNull();
        _ = manager.ActiveProfile.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenReplacementAndRollbackFail_FaultsTaskScopeAndRuntimeWithoutRestart()
    {
        using var mutations = new AutomationTaskMutationGate();
        var starts = 0;
        var running = false;
        var runtime = new AutomationRuntimeSession([new AutomationRuntimeComponent(
            "scheduler",
            () => running,
            _ =>
            {
                starts++;
                running = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                running = false;
                return Task.CompletedTask;
            })]);
        using var fixture = new ProfileCoordinatorFixture(_config.Path, mutations: mutations, runtimeSession: runtime);
        var manager = fixture.Coordinator;
        await manager.InitializeAsync();
        await runtime.StartAsync(TestContext.Current.CancellationToken);
        var previous = manager.ActiveProfile;
        var profile = await manager.CreateProfileAsync("Other");
        var previousScope = mutations.ScopeGeneration;
        var replacementError = new IOException("replacement failed");
        var rollbackError = new IOException("rollback failed");
        _ = fixture.Settings.ReloadAsync(manager.GetProfileDirectory(profile.Id)).Returns(Task.FromException(replacementError));
        _ = fixture.Settings.ReloadAsync(manager.GetProfileDirectory(previous.Id)).Returns(Task.FromException(rollbackError));

        var failure = await Assert.ThrowsAsync<AggregateException>(() => manager.SwitchProfileAsync(profile.Id));

        _ = failure.InnerExceptions.Should().ContainInOrder(replacementError, rollbackError);
        _ = manager.ActiveProfile.Id.Should().Be(previous.Id);
        Assert.True(mutations.ScopeGeneration > previousScope);
        Assert.True(mutations.IsFaulted);
        Assert.True(runtime.IsFaulted);
        Assert.False(running);
        Assert.Equal(1, starts);
        using var tasks = new ManageRunnableAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, Substitute.For<IAutomationTaskStore<ScheduledTask>>(), Substitute.For<IScheduledTaskOperations>().RunTaskAsync, mutations);
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.ListAsync(TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.AddAsync(new ScheduledTask(), mutations.ScopeGeneration, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<TaskScopeUnavailableException>(() => tasks.RunAsync(new TaskRequest(Guid.NewGuid()), TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync(TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunSuspendedAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task SwitchProfileAsync_AwaitsSchedulerShutdownBeforeReloadAndRestart()
    {
        using var fixture = new ProfileCoordinatorFixture(_config.Path, withScheduler: true);
        var scheduler = fixture.Scheduler!;
        var order = new List<string>();
        var schedulerRunning = true;
        _ = scheduler.IsRunning.Returns(_ => schedulerRunning);
        _ = scheduler.StopAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            schedulerRunning = false;
            order.Add("stop");
            return Task.CompletedTask;
        });
        _ = scheduler.LoadAsync().Returns(_ =>
        {
            order.Add("load");
            return Task.CompletedTask;
        });
        scheduler.When(service => service.Start()).Do(_ => order.Add("start"));
        await fixture.Coordinator.InitializeAsync();
        var profile = await fixture.Coordinator.CreateProfileAsync("Second Profile");
        order.Clear();

        await fixture.Coordinator.SwitchProfileAsync(profile.Id);

        _ = order.Should().Equal("stop", "load", "start");
    }

    [Fact]
    public async Task SwitchProfileAsync_AbortsWhenSchedulerLifetimeRemainsUnresolved()
    {
        using var fixture = new ProfileCoordinatorFixture(_config.Path, withScheduler: true);
        var scheduler = fixture.Scheduler!;
        var unresolvedLifetime = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = scheduler.IsRunning.Returns(returnThis: true);
        _ = scheduler.Completion.Returns(unresolvedLifetime.Task);
        _ = scheduler.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await fixture.Coordinator.InitializeAsync();
        var profile = await fixture.Coordinator.CreateProfileAsync("Second Profile");
        scheduler.ClearReceivedCalls();

        var act = () => fixture.Coordinator.SwitchProfileAsync(profile.Id);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scheduler did not quiesce*");
        await scheduler.DidNotReceive().LoadAsync();
        scheduler.DidNotReceive().Start();
        _ = unresolvedLifetime.TrySetResult();
    }

    [Fact]
    public async Task SwitchProfileAsync_FlushesAndReloadsProfileRuntimeParticipants()
    {
        var participant = Substitute.For<IProfileRuntimeParticipant>();
        using var fixture = new ProfileCoordinatorFixture(_config.Path, withScheduler: true, participants: [participant]);
        await fixture.Coordinator.InitializeAsync();
        var profile = await fixture.Coordinator.CreateProfileAsync("Second Profile");

        await fixture.Coordinator.SwitchProfileAsync(profile.Id);

        await participant.Received(1).FlushAsync(Arg.Any<CancellationToken>());
        await participant.Received(1).ReloadAsync(fixture.Coordinator.GetProfileDirectory(profile.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchProfileAsync_WhenParticipantReloadFails_RestoresPreviousProfileParticipantState()
    {
        var participant = Substitute.For<IProfileRuntimeParticipant>();
        using var fixture = new ProfileCoordinatorFixture(_config.Path, withScheduler: true, participants: [participant]);
        var manager = fixture.Coordinator;
        await manager.InitializeAsync();
        var profile = await manager.CreateProfileAsync("Second Profile");
        var previousProfileDirectory = manager.GetProfileDirectory(manager.ActiveProfile.Id);
        _ = participant.ReloadAsync(manager.GetProfileDirectory(profile.Id), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidDataException("Invalid loaded macro session."));

        var switchProfile = () => manager.SwitchProfileAsync(profile.Id);

        _ = await switchProfile.Should().ThrowAsync<InvalidDataException>();
        _ = manager.ActiveProfile.Id.Should().Be("default");
        await participant.Received(1).ReloadAsync(previousProfileDirectory, Arg.Any<CancellationToken>());
    }
}
