
namespace CrossMacro.Cli.Commands;

public sealed class ScreenshotCommandHandler(ScreenshotCommandService screenshotCommandService) : CliCommandHandlerBase<ScreenshotOptions>
{
    private readonly ScreenshotCommandService _screenshotCommandService = screenshotCommandService ?? throw new ArgumentNullException(nameof(screenshotCommandService));

    protected override Task<OperationResult> ExecuteAsync(ScreenshotOptions options, CancellationToken cancellationToken) =>
        _screenshotCommandService.ExecuteAsync(options, cancellationToken);
}
