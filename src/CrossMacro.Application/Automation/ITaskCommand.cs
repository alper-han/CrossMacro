namespace CrossMacro.Application.Automation;

/// <summary>The fields every automation task command carries; the rest is specific to the task kind.</summary>
public interface ITaskCommand
{
    public TaskCommandAction Action { get; }

    public string? TaskId { get; }

    public bool? Enabled { get; }
}
