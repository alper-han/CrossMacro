
namespace CrossMacro.Cli.Services;

public interface IScreenCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken);
}
