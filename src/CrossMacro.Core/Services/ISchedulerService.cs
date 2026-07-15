
namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro scheduling service
/// </summary>
public interface ISchedulerService : IDisposable
{
    /// <summary>
    /// Collection of scheduled tasks
    /// </summary>
    public ObservableCollection<ScheduledTask> Tasks { get; }

    /// <summary>
    /// Whether the scheduler is running
    /// </summary>
    public bool IsRunning { get; }

    /// <summary>
    /// Completes when the current timer loop and its serial execution have stopped.
    /// <see cref="StopScheduler"/> remains non-blocking for host shutdown compatibility.
    /// </summary>
    public Task Completion { get; }

    /// <summary>
    /// Adds a new scheduled task
    /// </summary>
    public void AddTask(ScheduledTask task);

    /// <summary>
    /// Removes a scheduled task by ID
    /// </summary>
    public void RemoveTask(Guid id);

    /// <summary>
    /// Updates an existing task
    /// </summary>
    public void UpdateTask(ScheduledTask task);

    /// <summary>
    /// Enables or disables a task
    /// </summary>
    public void SetTaskEnabled(Guid id, bool enabled);
    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the scheduler
    /// </summary>
    public void Start();

    /// <summary>
    /// Stops the scheduler
    /// </summary>
    public void StopScheduler();

    /// <summary>Requests shutdown and exposes completion of the current scheduler lifetime.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves tasks to persistent storage
    /// </summary>
    public Task SaveAsync();

    /// <summary>
    /// Loads tasks from persistent storage
    /// </summary>
    public Task LoadAsync();

    /// <summary>
    /// Event fired when a task is executed
    /// </summary>
    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;

    /// <summary>
    /// Event fired when a task starts executing
    /// </summary>
    public event EventHandler<ScheduledTaskStartingEventArgs>? TaskStarting;
}
