
namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for task execution events
/// </summary>
public class TaskExecutedEventArgs(
    ScheduledTask task,
    bool success,
    string? message = null) : EventArgs
{
    public ScheduledTask Task { get; } = task;
    public bool Success { get; } = success;
    public string? Message { get; } = message;
}
