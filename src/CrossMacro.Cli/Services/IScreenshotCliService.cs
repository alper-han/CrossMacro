
namespace CrossMacro.Cli.Services;

public interface IScreenshotCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken);
}
