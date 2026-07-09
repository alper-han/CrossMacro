using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class TriggerCommandHandler : CliCommandHandlerBase<TriggerCliOptions>
{
    private readonly ITriggerCliService _triggerCliService;

    public TriggerCommandHandler(ITriggerCliService triggerCliService)
    {
        _triggerCliService = triggerCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        return await _triggerCliService.ExecuteAsync(options, cancellationToken);
    }
}
