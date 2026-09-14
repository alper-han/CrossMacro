
namespace CrossMacro.Cli.Services.ScreenReading;

public interface IScreenCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken);
}
