namespace CrossMacro.UI.Tests;

internal static class UiAutomationTestComposition
{
    public static IManageTextExpansion Create(ITextExpansionStore store)
    {
        var profiles = Substitute.For<IProfileManager>();
        _ = profiles.ActiveProfile.Returns(new ProfileInfo { Id = "default" });
        return new ManageTextExpansion(store, profiles, Substitute.For<IProfileTextExpansionStore>(), new AutomationTaskMutationGate());
    }

    public static IManageSchedule Create(ISchedulerService service, AutomationTaskMutationGate? mutationGate = null, bool alreadyLoaded = false)
    {
        _ = service.IsCurrentTask(Arg.Any<ScheduledTask>()).Returns(call => service.Tasks.Any(active => ReferenceEquals(active, call.Arg<ScheduledTask>())));
        var adapter = new ScheduleStore(service, alreadyLoaded);
        return new ManageSchedule(adapter, adapter, mutationGate: mutationGate);
    }

    public static IManageShortcut Create(IShortcutService service, AutomationTaskMutationGate? mutationGate = null, bool alreadyLoaded = false)
    {
        _ = service.IsCurrentTask(Arg.Any<ShortcutTask>()).Returns(call => service.Tasks.Any(active => ReferenceEquals(active, call.Arg<ShortcutTask>())));
        var adapter = new ShortcutStore(service, alreadyLoaded);
        return new ManageShortcut(adapter, adapter, mutationGate: mutationGate);
    }

    public static IManageTrigger Create(ITriggerService service, AutomationTaskMutationGate? mutationGate = null, bool alreadyLoaded = false)
    {
        _ = service.IsCurrentTask(Arg.Any<TriggerTask>()).Returns(call => service.Tasks.Any(active => ReferenceEquals(active, call.Arg<TriggerTask>())));
        var adapter = new TriggerStore(service, alreadyLoaded);
        return new ManageTrigger(adapter, mutationGate: mutationGate);
    }

    /// <summary>Commits automation task snapshots into the substituted service's live task collection.</summary>
    internal abstract class ServiceTaskStore<TTask>(
        ICollection<TTask> serviceTasks,
        Func<TTask, Guid> idOf,
        Func<Task> loadService,
        Func<Task> saveService,
        Action<TTask, TTask> copyTo,
        bool alreadyLoaded)
    {
        private bool _loaded = alreadyLoaded;

        public IReadOnlyList<TTask> Tasks => serviceTasks.ToArray();

        public async Task LoadAsync()
        {
            if (_loaded)
            {
                return;
            }

            await loadService();
            _loaded = true;
        }

        public Task SaveAsync() => saveService();

        public async Task CommitAsync(IReadOnlyList<TTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await saveService();
            foreach (var existing in serviceTasks.ToArray())
            {
                if (!tasks.Any(task => idOf(task) == idOf(existing)))
                {
                    _ = serviceTasks.Remove(existing);
                }
            }

            foreach (var task in tasks)
            {
                var existing = serviceTasks.FirstOrDefault(item => idOf(item) == idOf(task));
                if (existing is null)
                {
                    serviceTasks.Add(task);
                }
                else
                {
                    copyTo(task, existing);
                }
            }
        }
    }

    private sealed class ScheduleStore(ISchedulerService service, bool alreadyLoaded)
        : ServiceTaskStore<ScheduledTask>(service.Tasks, task => task.Id, service.LoadAsync, service.SaveAsync, AutomationTaskSnapshots.CopyTo, alreadyLoaded),
          IScheduledTaskOperations, IScheduledTaskStore
    {
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);
    }

    private sealed class ShortcutStore(IShortcutService service, bool alreadyLoaded)
        : ServiceTaskStore<ShortcutTask>(service.Tasks, task => task.Id, service.LoadAsync, service.SaveAsync, AutomationTaskSnapshots.CopyTo, alreadyLoaded),
          IShortcutTaskOperations, IShortcutTaskStore
    {
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);
    }

    private sealed class TriggerStore(ITriggerService service, bool alreadyLoaded)
        : ServiceTaskStore<TriggerTask>(service.Tasks, task => task.Id, service.LoadAsync, service.SaveAsync, AutomationTaskSnapshots.CopyTo, alreadyLoaded),
          ITriggerTaskStore;
}
