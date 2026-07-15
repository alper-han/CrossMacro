
namespace CrossMacro.Cli.Commands;

public sealed class ScreenshotCommandHandler : CliCommandHandlerBase<ScreenshotCliOptions>
{
    private readonly IScreenshotCliService _screenshotCliService;

    public ScreenshotCommandHandler(IScreenshotCliService screenshotCliService)
    {
        _screenshotCliService = screenshotCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken) =>
        _screenshotCliService.ExecuteAsync(options, cancellationToken);
}
