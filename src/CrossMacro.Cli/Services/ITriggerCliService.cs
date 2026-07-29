
namespace CrossMacro.Cli.Services;

public interface ITriggerCliService
{
    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken);
}
