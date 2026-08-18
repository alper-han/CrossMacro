
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignSchedulerService : ISchedulerService
{
    public DesignSchedulerService()
    {
        Tasks = new ObservableCollection<ScheduledTask>(DesignPreviewSamples.CreateScheduledTasks());
    }

    public ObservableCollection<ScheduledTask> Tasks { get; }

    public bool IsRunning { get; private set; }

    public Task Completion => Task.CompletedTask;

    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;

    public event EventHandler<ScheduledTaskStartingEventArgs>? TaskStarting;

    public void AddTask(ScheduledTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            _ = Tasks.Remove(task);
        }
    }

    public void UpdateTask(ScheduledTask task) { /* Empty */ }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        _ = task?.IsEnabled = enabled;
    }

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is not null)
        {
            TaskStarting?.Invoke(this, new ScheduledTaskStartingEventArgs(task));
            TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, success: true, message: "Preview macro run completed"));
        }

        return Task.CompletedTask;
    }

    public void Start() => IsRunning = true;

    public void StopScheduler() => IsRunning = false;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopScheduler();
        return Task.CompletedTask;
    }

    public Task SaveAsync() => Task.CompletedTask;

    public Task LoadAsync() => Task.CompletedTask;

    public void Dispose() { /* Empty */ }
}
