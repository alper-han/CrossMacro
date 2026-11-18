
namespace CrossMacro.Cli;

public interface ICliCommandHandler
{
    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken);
}
