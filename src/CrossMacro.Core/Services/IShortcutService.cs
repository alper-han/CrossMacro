
namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro shortcut service
/// </summary>
public interface IShortcutService : IDisposable
{
    /// <summary>
    /// Collection of shortcut tasks
    /// </summary>
    public ObservableCollection<ShortcutTask> Tasks { get; }

    /// <summary>
    /// Whether the service is listening for shortcuts
    /// </summary>
    public bool IsListening { get; }

    /// <summary>
    /// Adds a new shortcut task
    /// </summary>
    public void AddTask(ShortcutTask task);

    /// <summary>
    /// Removes a shortcut task by ID
    /// </summary>
    public void RemoveTask(Guid id);

    /// <summary>
    /// Updates an existing task
    /// </summary>
    public void UpdateTask(ShortcutTask task);

    /// <summary>
    /// Enables or disables a task
    /// </summary>
    public void SetTaskEnabled(Guid id, bool enabled);

    /// <summary>
    /// Starts listening for shortcuts
    /// </summary>
    public void Start();

    /// <summary>
    /// Stops listening for shortcuts
    /// </summary>
    public void StopShortcuts();

    /// <summary>
    /// Saves tasks to persistent storage
    /// </summary>
    public Task SaveAsync();

    /// <summary>
    /// Runs a shortcut task manually by id.
    /// </summary>
    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads tasks from persistent storage
    /// </summary>
    public Task LoadAsync();

    /// <summary>
    /// Reloads tasks from the supplied profile configuration directory.
    /// </summary>
    public Task ReloadAsync(string profileConfigDirectory) => LoadAsync();

    /// <summary>
    /// Event fired when a shortcut is executed
    /// </summary>
    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

    /// <summary>
    /// Event fired when a shortcut starts executing
    /// </summary>
    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;
}
