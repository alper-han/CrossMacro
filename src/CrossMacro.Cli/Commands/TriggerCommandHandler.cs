
namespace CrossMacro.Cli.Commands;

public sealed class TriggerCommandHandler(ITriggerCliService triggerCliService) : CliCommandHandlerBase<TriggerCliOptions>
{
    private readonly ITriggerCliService _triggerCliService = triggerCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        return await _triggerCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
