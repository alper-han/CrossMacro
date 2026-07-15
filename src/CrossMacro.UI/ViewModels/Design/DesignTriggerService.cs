
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignTriggerService : ITriggerService
{
    public DesignTriggerService()
    {
        Tasks = new ObservableCollection<TriggerTask>(DesignPreviewSamples.CreateTriggerTasks());
    }

    public ObservableCollection<TriggerTask> Tasks { get; }

    public bool IsMonitoring { get; private set; }

    public Task Completion => Task.CompletedTask;

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired { add { } remove { } }

    public void AddTask(TriggerTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            Tasks.Remove(task);
        }
    }

    public void UpdateTask(TriggerTask task) { }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            task.IsEnabled = enabled;
        }
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

    public void Dispose() { }
}
