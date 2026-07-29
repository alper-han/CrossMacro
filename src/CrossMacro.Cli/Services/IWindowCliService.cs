
namespace CrossMacro.Cli.Services;

public interface IWindowCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken);
}
