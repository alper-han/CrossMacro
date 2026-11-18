namespace CrossMacro.Infrastructure.Tests.Services.Automation;

public sealed class AutomationTaskStoreTests
{
    [Fact]
    public async Task ShortcutService_AfterFailedLoad_RefusesToOverwriteTheFileUntilALoadSucceeds()
    {
        var repository = Substitute.For<IShortcutTaskRepository>();
        _ = repository.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ShortcutTask>?>(new IOException("locked")), Task.FromResult<IReadOnlyList<ShortcutTask>?>([]));
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), repository);

        await service.LoadAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitAsync([], TestContext.Current.CancellationToken));
        _ = repository.DidNotReceive().SaveAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());

        await service.LoadAsync();
        await service.SaveAsync();

        _ = repository.Received(1).SaveAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerService_AfterFailedLoad_RefusesToOverwriteTheFileUntilALoadSucceeds()
    {
        var repository = Substitute.For<ITriggerTaskRepository>();
        _ = repository.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TriggerTask>?>(new IOException("locked")), Task.FromResult<IReadOnlyList<TriggerTask>?>([]));
        using var service = new TriggerService(windowManager: null, Substitute.For<IProfileSwitchRequests>(), Substitute.For<IMacroFileManager>(),
            () => Substitute.For<IMacroPlayer>(), TimeProvider.System, repository);

        await service.LoadAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitAsync([], TestContext.Current.CancellationToken));
        _ = repository.DidNotReceive().SaveAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());

        await service.LoadAsync();
        await service.SaveAsync();

        _ = repository.Received(1).SaveAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SchedulerService_AfterFailedLoad_RefusesToOverwriteTheFileUntilALoadSucceeds()
    {
        var repository = Substitute.For<IScheduledTaskRepository>();
        _ = repository.LoadAsync()
            .Returns(Task.FromException<IReadOnlyList<ScheduledTask>>(new IOException("locked")), Task.FromResult<IReadOnlyList<ScheduledTask>>([]));
        using var service = new SchedulerService(repository, Substitute.For<IScheduledTaskExecutor>(), TimeProvider.System);

        await service.LoadAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitAsync([], TestContext.Current.CancellationToken));
        _ = repository.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<ScheduledTask>>(), Arg.Any<CancellationToken>());

        await service.LoadAsync();
        await service.SaveAsync();

        _ = repository.Received(1).SaveAsync(Arg.Any<IEnumerable<ScheduledTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_ReordersAndReconfiguresLiveTasksWithoutReplacingTheirIdentity()
    {
        var repository = Substitute.For<IShortcutTaskRepository>();
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), repository);
        var first = new ShortcutTask { Name = "First" };
        var second = new ShortcutTask { Name = "Second" };
        var third = new ShortcutTask { Name = "Third" };
        service.Tasks.Add(first);
        service.Tasks.Add(second);
        service.Tasks.Add(third);
        var renamedThird = AutomationTaskSnapshots.Copy(third);
        renamedThird.Name = "Renamed";

        await service.CommitAsync([renamedThird, AutomationTaskSnapshots.Copy(first)], TestContext.Current.CancellationToken);

        Assert.Equal([third, first], service.Tasks);
        Assert.Equal("Renamed", third.Name);
    }
}
