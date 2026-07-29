
namespace CrossMacro.Cli;

public interface ICliCommandHandler
{
    public bool CanHandle(CliCommandOptions options);

    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken);
}
