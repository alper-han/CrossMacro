using System;
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
