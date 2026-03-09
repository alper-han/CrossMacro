using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Responsible for executing scheduled tasks
/// </summary>
public interface IScheduledTaskExecutor
{
    /// <summary>
    /// Executes a single scheduled task
    /// </summary>
    Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event fired when a task execution is completed
    /// </summary>
    event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
    
    /// <summary>
    /// Event fired when a task is starting
    /// </summary>
    event EventHandler<ScheduledTask>? TaskStarting;
}
