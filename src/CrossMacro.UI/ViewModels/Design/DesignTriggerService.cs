
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignTriggerService : ITriggerService, ITriggerTaskOperations, ITriggerTaskStore
{
    IReadOnlyList<TriggerTask> ITriggerTaskStore.Tasks => Tasks;

    public Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Tasks.Clear();
        foreach (var task in tasks) { Tasks.Add(AutomationTaskSnapshots.Copy(task)); }
        return Task.CompletedTask;
    }

    public DesignTriggerService()
    {
        Tasks = new ObservableCollection<TriggerTask>(DesignPreviewSamples.CreateTriggerTasks());
    }

    public ObservableCollection<TriggerTask> Tasks { get; }

    public bool IsCurrentTask(TriggerTask task) => Tasks.Any(active => ReferenceEquals(active, task));

    public bool IsMonitoring { get; private set; }

    public Task Completion => Task.CompletedTask;

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired { add { /* Empty */ } remove { /* Empty */ } }

    public void AddTask(TriggerTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            _ = Tasks.Remove(task);
        }
    }

    public void UpdateTask(TriggerTask task) { /* Empty */ }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        _ = task?.IsEnabled = enabled;
    }

    public void Start() => IsMonitoring = true;

    public void StopMonitoring() => IsMonitoring = false;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopMonitoring();
        return Task.CompletedTask;
    }

    public Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync() => Task.CompletedTask;

    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;

    public void Dispose() { /* Empty */ }
}
