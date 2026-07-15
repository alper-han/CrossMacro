
namespace CrossMacro.Cli.Services;

public interface IWindowCliService
{
    Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken);
}
