namespace CrossMacro.Application.Automation;

/// <summary>The active profile could not be restored and its task stores are unavailable.</summary>
public sealed class TaskScopeUnavailableException : InvalidOperationException
{
    public const string UnavailableMessage = "The active profile could not be restored. Restart the application before changing or running tasks.";

    public TaskScopeUnavailableException() : base(UnavailableMessage) { }
    public TaskScopeUnavailableException(string? message) : base(message) { }
    public TaskScopeUnavailableException(string? message, Exception? innerException) : base(message, innerException) { }
}
