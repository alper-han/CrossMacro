using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro scheduling service
/// </summary>
public interface ISchedulerService : IDisposable
{
    /// <summary>
    /// Collection of scheduled tasks
    /// </summary>
    ObservableCollection<ScheduledTask> Tasks { get; }

    /// <summary>
    /// Whether the scheduler is running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Completes when the current timer loop and its serial execution have stopped.
    /// <see cref="Stop"/> remains non-blocking for host shutdown compatibility.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Adds a new scheduled task
    /// </summary>
    void AddTask(ScheduledTask task);

    /// <summary>
    /// Removes a scheduled task by ID
    /// </summary>
    void RemoveTask(Guid id);

    /// <summary>
    /// Updates an existing task
    /// </summary>
    void UpdateTask(ScheduledTask task);

    /// <summary>
    /// Enables or disables a task
    /// </summary>
    void SetTaskEnabled(Guid id, bool enabled);
    Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the scheduler
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the scheduler
    /// </summary>
    void Stop();

    /// <summary>Requests shutdown and exposes completion of the current scheduler lifetime.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves tasks to persistent storage
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Loads tasks from persistent storage
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Event fired when a task is executed
    /// </summary>
    event EventHandler<TaskExecutedEventArgs>? TaskExecuted;

    /// <summary>
    /// Event fired when a task starts executing
    /// </summary>
    event EventHandler<ScheduledTask>? TaskStarting;
}
