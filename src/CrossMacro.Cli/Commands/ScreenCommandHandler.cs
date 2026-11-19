
namespace CrossMacro.Cli.Commands;

public sealed class ScreenCommandHandler(ScreenCommandService screenCommandService) : CliCommandHandlerBase<ScreenOptions>
{
    private readonly ScreenCommandService _screenCommandService = screenCommandService ?? throw new ArgumentNullException(nameof(screenCommandService));

    protected override Task<OperationResult> ExecuteAsync(ScreenOptions options, CancellationToken cancellationToken) =>
        _screenCommandService.ExecuteAsync(options, cancellationToken);
}
