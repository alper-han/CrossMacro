
namespace CrossMacro.Cli.Commands;

public sealed class ScreenshotCommandHandler(IScreenshotCommandService screenshotCommandService) : CliCommandHandlerBase<ScreenshotOptions>
{
    private readonly IScreenshotCommandService _screenshotCommandService = screenshotCommandService ?? throw new ArgumentNullException(nameof(screenshotCommandService));

    protected override Task<OperationResult> ExecuteAsync(ScreenshotOptions options, CancellationToken cancellationToken) =>
        _screenshotCommandService.ExecuteAsync(options, cancellationToken);
}
