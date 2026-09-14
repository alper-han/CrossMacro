namespace CrossMacro.UI.Tests;

internal static class UiAutomationTestComposition
{
    public static IManageTextExpansion Create(ITextExpansionStore store)
    {
        var profiles = Substitute.For<IProfileManager>();
        _ = profiles.ActiveProfile.Returns(new ProfileInfo { Id = "default" });
        return new ManageTextExpansion(store, profiles);
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
        return new ManageTrigger(adapter, adapter, mutationGate: mutationGate);
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
        public void AddTask(ScheduledTask task) => service.AddTask(task);
        public void UpdateTask(ScheduledTask task) => service.UpdateTask(task);
        public void RemoveTask(Guid id) => service.RemoveTask(id);
        public void SetTaskEnabled(Guid id, bool enabled) => service.SetTaskEnabled(id, enabled);
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);

        public async Task CommitAsync(IReadOnlyList<ScheduledTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    service.RemoveTask(existing.Id);
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.AddTask(task);
                    if (!service.Tasks.Any(item => item.Id == task.Id)) { service.Tasks.Add(task); }
                }
                else
                {
                    if (existing.IsEnabled != task.IsEnabled) { service.SetTaskEnabled(task.Id, task.IsEnabled); }
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
        public void AddTask(ShortcutTask task) => service.AddTask(task);
        public void UpdateTask(ShortcutTask task) => service.UpdateTask(task);
        public void RemoveTask(Guid id) => service.RemoveTask(id);
        public void SetTaskEnabled(Guid id, bool enabled) => service.SetTaskEnabled(id, enabled);
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => service.RunTaskAsync(taskId, cancellationToken);

        public async Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    service.RemoveTask(existing.Id);
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.AddTask(task);
                    if (!service.Tasks.Any(item => item.Id == task.Id)) { service.Tasks.Add(task); }
                }
                else
                {
                    if (existing.IsEnabled != task.IsEnabled) { service.SetTaskEnabled(task.Id, task.IsEnabled); }
                    AutomationTaskSnapshots.CopyTo(task, existing);
                }
            }
        }
    }



    private sealed class TriggerStore(ITriggerService service, bool alreadyLoaded) : ITriggerTaskOperations, ITriggerTaskStore
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
        public void AddTask(TriggerTask task) => service.AddTask(task);
        public void UpdateTask(TriggerTask task) => service.UpdateTask(task);
        public void RemoveTask(Guid id) => service.RemoveTask(id);
        public void SetTaskEnabled(Guid id, bool enabled) => service.SetTaskEnabled(id, enabled);

        public async Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await service.SaveAsync();
            foreach (var existing in service.Tasks.ToArray())
            {
                if (!tasks.Any(task => task.Id == existing.Id))
                {
                    service.RemoveTask(existing.Id);
                    _ = service.Tasks.Remove(existing);
                }
            }
            foreach (var task in tasks)
            {
                var existing = service.Tasks.FirstOrDefault(item => item.Id == task.Id);
                if (existing is null)
                {
                    service.AddTask(task);
                    if (!service.Tasks.Any(item => item.Id == task.Id)) { service.Tasks.Add(task); }
                }
                else
                {
                    if (existing.IsEnabled != task.IsEnabled) { service.SetTaskEnabled(task.Id, task.IsEnabled); }
                    AutomationTaskSnapshots.CopyTo(task, existing);
                }
            }
        }
    }
}
