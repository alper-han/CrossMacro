namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class AutomationTaskIdentityTests
{
    [Fact]
    public void SchedulerObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        using var service = new SchedulerService(Substitute.For<IScheduledTaskRepository>(), Substitute.For<IScheduledTaskExecutor>(), TimeProvider.System);
        var original = new ScheduledTask();
        var replacement = new ScheduledTask { Id = original.Id };
        service.AddTask(original);
        Assert.True(service.IsCurrentTask(original));
        Assert.False(service.IsCurrentTask(replacement));
        service.RemoveTask(original.Id);
        service.AddTask(replacement);
        Assert.False(service.IsCurrentTask(original));
        Assert.True(service.IsCurrentTask(replacement));
    }

    [Fact]
    public void ShortcutObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        using var service = new ShortcutService(Substitute.For<IMacroFileManager>(), () => Substitute.For<IMacroPlayer>(),
            Substitute.For<IGlobalHotkeyService>(), shortcutsFilePath: "unused.json", taskRepository: Substitute.For<IShortcutTaskRepository>());
        var original = new ShortcutTask();
        var replacement = new ShortcutTask { Id = original.Id };
        service.AddTask(original);
        Assert.True(service.IsCurrentTask(original));
        Assert.False(service.IsCurrentTask(replacement));
        service.RemoveTask(original.Id);
        service.AddTask(replacement);
        Assert.False(service.IsCurrentTask(original));
        Assert.True(service.IsCurrentTask(replacement));
    }

    [Fact]
    public void TriggerObservation_RejectsDetachedAndReplacedObjectsWithTheSameId()
    {
        using var service = new TriggerService(windowManager: null, Substitute.For<IProfileSwitchRequests>(), Substitute.For<IMacroFileManager>(),
            () => Substitute.For<IMacroPlayer>(), "unused.json", TimeProvider.System, Substitute.For<ITriggerTaskRepository>());
        var original = new TriggerTask();
        var replacement = new TriggerTask { Id = original.Id };
        service.AddTask(original);
        Assert.True(service.IsCurrentTask(original));
        Assert.False(service.IsCurrentTask(replacement));
        service.RemoveTask(original.Id);
        service.AddTask(replacement);
        Assert.False(service.IsCurrentTask(original));
        Assert.True(service.IsCurrentTask(replacement));
    }
}
