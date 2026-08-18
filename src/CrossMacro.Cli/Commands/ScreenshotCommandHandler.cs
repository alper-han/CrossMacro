
namespace CrossMacro.Cli.Commands;

public sealed class ScreenshotCommandHandler(IScreenshotCliService screenshotCliService) : CliCommandHandlerBase<ScreenshotCliOptions>
{
    private readonly IScreenshotCliService _screenshotCliService = screenshotCliService;

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken) =>
        _screenshotCliService.ExecuteAsync(options, cancellationToken);
}
