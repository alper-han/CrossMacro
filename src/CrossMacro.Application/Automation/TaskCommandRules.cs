namespace CrossMacro.Application.Automation;

/// <summary>
/// How one task kind turns its command fields into task changes. <see cref="TaskCommands{TTask,TCommand}"/>
/// owns loading, scope checks, enabling, committing and the result messages.
/// </summary>
public abstract class TaskCommandRules<TTask, TCommand>
    where TTask : class
    where TCommand : ITaskCommand
{
    /// <summary>Fills a default-constructed task from an add command; returns a failure, or null when the task is valid.</summary>
    public abstract TaskCommandResult<TTask>? ApplyAdd(TTask task, TCommand command);

    /// <summary>Applies the fields an edit command sets to a detached copy; returns a failure, or null on success.</summary>
    public abstract TaskCommandResult<TTask>? ApplyEdit(TTask task, TCommand command);

    /// <summary>Applies a <see cref="TaskCommandAction.Bind"/> command; null when the kind has no such action.</summary>
    public virtual Func<TTask, TCommand, TaskCommandResult<TTask>?>? Bind => null;

    /// <summary>Describes the next run for a <see cref="TaskCommandAction.Next"/> command; null when the kind has no such action.</summary>
    public virtual Func<TTask, string>? DescribeNext => null;
}
