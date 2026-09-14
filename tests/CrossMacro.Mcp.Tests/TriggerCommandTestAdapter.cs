using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tests;

internal sealed class TriggerCommandTestAdapter(ITriggerCliService service) : ITriggerCommands
{
    public async Task<TaskCommandResult<TriggerTask>> ListAsync(CancellationToken cancellationToken) =>
        TaskCommandTestResults.Trigger(await service.ListAsync(cancellationToken));
    public async Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken) =>
        TaskCommandTestResults.Trigger(await service.ExecuteAsync(new TriggerCliOptions(Enum.Parse<TriggerCliAction>(options.Action.ToString()), TaskId: options.TaskId), cancellationToken));
}
