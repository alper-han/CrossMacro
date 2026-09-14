namespace CrossMacro.Application.Automation;

/// <summary>The draft belongs to an earlier active profile and must be reloaded.</summary>
public sealed class TaskScopeConflictException : InvalidOperationException
{
    public const string ConflictMessage = "The active profile changed. Reload the task and try again.";

    public TaskScopeConflictException() : base(ConflictMessage) { }
    public TaskScopeConflictException(string? message) : base(message) { }
    public TaskScopeConflictException(string? message, Exception? innerException) : base(message, innerException) { }
}
