
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignShortcutService : IShortcutService, IShortcutTaskOperations, IShortcutTaskStore
{
    IReadOnlyList<ShortcutTask> IShortcutTaskStore.Tasks => Tasks;

    public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Tasks.Clear();
        foreach (var task in tasks) { Tasks.Add(AutomationTaskSnapshots.Copy(task)); }
        return Task.CompletedTask;
    }

    public DesignShortcutService()
    {
        Tasks = new ObservableCollection<ShortcutTask>(DesignPreviewSamples.CreateShortcutTasks());
    }

    public ObservableCollection<ShortcutTask> Tasks { get; }

    public bool IsCurrentTask(ShortcutTask task) => Tasks.Any(active => ReferenceEquals(active, task));

    public bool IsListening { get; private set; }

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;

    public void Start() => IsListening = true;

    public void StopShortcuts() => IsListening = false;

    public Task SaveAsync() => Task.CompletedTask;

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is not null)
        {
            ShortcutStarting?.Invoke(this, new ShortcutStartingEventArgs(task));
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: true, message: "Preview macro run completed"));
        }

        return Task.CompletedTask;
    }

    public Task LoadAsync() => Task.CompletedTask;

    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;

    public void Dispose() { /* Empty */ }
}
