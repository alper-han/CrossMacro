namespace CrossMacro.Application.Automation;

/// <summary>The command set of task kinds that can also be run on demand (schedules and shortcuts).</summary>
public interface IRunnableTaskCommands<TTask, TCommand> : ITaskCommands<TTask, TCommand>
    where TTask : class
    where TCommand : ITaskCommand
{
    public Task<TaskCommandResult<TTask>> RunAsync(string taskId, CancellationToken cancellationToken);
}
