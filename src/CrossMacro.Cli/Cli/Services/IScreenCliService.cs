
namespace CrossMacro.Cli.Services;

public interface IScreenCliService
{
    Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken);
}
