
namespace CrossMacro.Cli.Commands;

public sealed class TriggerListCommandHandler : CliCommandHandlerBase<TriggerListCliOptions>
{
    private readonly ITriggerCliService _triggerCliService;

    public TriggerListCommandHandler(ITriggerCliService triggerCliService)
    {
        _triggerCliService = triggerCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TriggerListCliOptions options, CancellationToken cancellationToken)
    {
        return await _triggerCliService.ListAsync(cancellationToken).ConfigureAwait(false);
    }
}
