
namespace CrossMacro.Cli.Commands;

public sealed class ScreenCommandHandler(IScreenCommandService screenCommandService) : CliCommandHandlerBase<ScreenOptions>
{
    private readonly IScreenCommandService _screenCommandService = screenCommandService ?? throw new ArgumentNullException(nameof(screenCommandService));

    protected override Task<OperationResult> ExecuteAsync(ScreenOptions options, CancellationToken cancellationToken) =>
        _screenCommandService.ExecuteAsync(options, cancellationToken);
}
