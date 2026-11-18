namespace CrossMacro.Application.Automation;

/// <summary>The command set of one automation task kind, as used by the CLI and MCP hosts.</summary>
public interface ITaskCommands<TTask, TCommand>
    where TTask : class
    where TCommand : ITaskCommand
{
    public Task<TaskCommandResult<TTask>> ListAsync(CancellationToken cancellationToken);
    public Task<TaskCommandResult<TTask>> ExecuteAsync(TCommand options, CancellationToken cancellationToken);
}
