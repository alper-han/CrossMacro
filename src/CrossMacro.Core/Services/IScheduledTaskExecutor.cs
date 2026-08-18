
namespace CrossMacro.Core.Services;

/// <summary>
/// Responsible for executing scheduled tasks
/// </summary>
public interface IScheduledTaskExecutor
{
    /// <summary>
    /// Executes a single scheduled task
    /// </summary>
    public Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when a task execution is completed
    /// </summary>
    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;

    /// <summary>
    /// Event fired when a task is starting
    /// </summary>
    public event EventHandler<ScheduledTaskStartingEventArgs>? TaskStarting;
}
