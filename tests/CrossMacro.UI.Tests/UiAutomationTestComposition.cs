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

    private sealed class ScheduleStore(ISchedulerService service, bool alreadyLoaded) : IScheduledTaskOperations, IScheduledTaskStore
    {
        public IReadOnlyList<ScheduledTask> Tasks => service.Tasks.ToArray();
        private bool _loaded = alreadyLoaded;
        public async Task LoadAsync()
        {
            if (_loaded) { return; }
            await service.LoadAsync();
            _loaded = true;
        }
        public Task SaveAsync() => service.SaveAsync();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);

        public async Task CommitAsync(IReadOnlyList<ScheduledTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.Tasks.Add(task);
                }
                else
                {
                    AutomationTaskSnapshots.CopyTo(task, existing);
                }
            }
        }
    }

    private sealed class ShortcutStore(IShortcutService service, bool alreadyLoaded) : IShortcutTaskOperations, IShortcutTaskStore
    {
        public IReadOnlyList<ShortcutTask> Tasks => service.Tasks.ToArray();
        private bool _loaded = alreadyLoaded;
        public async Task LoadAsync()
        {
            if (_loaded) { return; }
            await service.LoadAsync();
            _loaded = true;
        }
        public Task SaveAsync() => service.SaveAsync();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);

        public async Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.Tasks.Add(task);
                }
                else
                {
                    AutomationTaskSnapshots.CopyTo(task, existing);
                }
            }
        }
    }

    private sealed class TriggerStore(ITriggerService service, bool alreadyLoaded) : ITriggerTaskStore
    {
        public IReadOnlyList<TriggerTask> Tasks => service.Tasks.ToArray();
        private bool _loaded = alreadyLoaded;
        public async Task LoadAsync()
        {
            if (_loaded) { return; }
            await service.LoadAsync();
            _loaded = true;
        }
        public Task SaveAsync() => service.SaveAsync();

        public async Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.Tasks.Add(task);
                }
                else
                {
                    AutomationTaskSnapshots.CopyTo(task, existing);
                }
            }
        }
    }
}
