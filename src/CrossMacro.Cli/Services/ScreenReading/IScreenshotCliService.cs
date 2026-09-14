
namespace CrossMacro.Cli.Services.ScreenReading;

public interface IScreenshotCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken);
}
