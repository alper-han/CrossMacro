
namespace CrossMacro.Cli.Services;

public interface IScreenshotCliService
{
    Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken);
}
