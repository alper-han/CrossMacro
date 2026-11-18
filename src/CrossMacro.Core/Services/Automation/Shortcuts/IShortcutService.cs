
namespace CrossMacro.Core.Services.Automation.Shortcuts;

/// <summary>
/// Interface for macro shortcut service
/// </summary>
public interface IShortcutService : IDisposable
{
    /// <summary>
    /// Collection of shortcut tasks
    /// </summary>
    public ObservableCollection<ShortcutTask> Tasks { get; }

    /// <summary>Checks runtime object identity under the service's task synchronization.</summary>
    public bool IsCurrentTask(ShortcutTask task);

    /// <summary>
    /// Whether the service is listening for shortcuts
    /// </summary>
    public bool IsListening { get; }

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
    public Task ReloadAsync(string profileConfigDirectory);

    /// <summary>
    /// Event fired when a shortcut is executed
    /// </summary>
    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

    /// <summary>
    /// Event fired when a shortcut starts executing
    /// </summary>
    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;
}
