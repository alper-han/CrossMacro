namespace CrossMacro.Infrastructure.Tests.Services.Automation;

public sealed class AutomationTaskIdentityTests
{
    [Fact]
    public void SchedulerObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        using var service = new SchedulerService(Substitute.For<IScheduledTaskRepository>(), Substitute.For<IScheduledTaskExecutor>(), TimeProvider.System);
        var original = new ScheduledTask();
        var replacement = new ScheduledTask { Id = original.Id };

        AssertObservationFollowsObjectIdentity(
            service.Tasks,
            service.IsCurrentTask,
            original,
            replacement);
    }

    [Fact]
    public void ShortcutObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        using var service = new ShortcutService(
            Substitute.For<IMacroFileManager>(),
            () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(),
            taskRepository: Substitute.For<IShortcutTaskRepository>());
        var original = new ShortcutTask();
        var replacement = new ShortcutTask { Id = original.Id };

        AssertObservationFollowsObjectIdentity(
            service.Tasks,
            service.IsCurrentTask,
            original,
            replacement);
    }

    [Fact]
    public async Task TriggerObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        await using var service = new TriggerService(
            windowManager: null,
            Substitute.For<IProfileSwitchRequests>(),
            Substitute.For<IMacroFileManager>(),
            () => Substitute.For<IMacroPlayer>(),
            TimeProvider.System,
            Substitute.For<ITriggerTaskRepository>());
        var original = new TriggerTask();
        var replacement = new TriggerTask { Id = original.Id };

        AssertObservationFollowsObjectIdentity(
            service.Tasks,
            service.IsCurrentTask,
            original,
            replacement);
    }

    private static void AssertObservationFollowsObjectIdentity<TTask>(
        ICollection<TTask> tasks,
        Func<TTask, bool> isCurrentTask,
        TTask original,
        TTask replacement)
    {
        tasks.Add(original);
        Assert.True(isCurrentTask(original));
        Assert.False(isCurrentTask(replacement));

        _ = tasks.Remove(original);
        tasks.Add(replacement);

        Assert.False(isCurrentTask(original));
        Assert.True(isCurrentTask(replacement));
    }
}
