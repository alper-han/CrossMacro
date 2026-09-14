namespace CrossMacro.Application.Automation;

public interface IShortcutCommands
{
    public Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken);
    public Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken);
    public Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken);
}
