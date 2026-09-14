using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tests;

internal sealed class ShortcutCommandTestAdapter(IShortcutCliService service) : IShortcutCommands
{
    public async Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken) =>
        TaskCommandTestResults.Shortcut(await service.ListAsync(cancellationToken));
    public async Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken) =>
        TaskCommandTestResults.Shortcut(await service.RunAsync(taskId, cancellationToken));
    public async Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken) =>
        TaskCommandTestResults.Shortcut(await service.ExecuteAsync(new ShortcutCliOptions(Enum.Parse<ShortcutCliAction>(options.Action.ToString()), TaskId: options.TaskId), cancellationToken));
}
