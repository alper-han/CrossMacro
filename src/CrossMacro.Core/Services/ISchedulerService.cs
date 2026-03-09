using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for task execution events
/// </summary>
public class TaskExecutedEventArgs : EventArgs
{
    public ScheduledTask Task { get; }
    public bool Success { get; }
    public string? Message { get; }
    
    public TaskExecutedEventArgs(ScheduledTask task, bool success, string? message = null)
    {
        Task = task;
        Success = success;
        Message = message;
    }
}

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
