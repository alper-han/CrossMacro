
namespace CrossMacro.Cli.Services;

public interface ITriggerCliService
{
    Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken);
}
