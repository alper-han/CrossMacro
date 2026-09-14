namespace CrossMacro.Application.Automation;

public interface ITriggerCommands
{
    public Task<TaskCommandResult<TriggerTask>> ListAsync(CancellationToken cancellationToken);
    public Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken);
}
