
namespace CrossMacro.Cli.Commands;

public sealed class ScreenCommandHandler(IScreenCliService screenCliService) : CliCommandHandlerBase<ScreenCliOptions>
{
    private readonly IScreenCliService _screenCliService = screenCliService;

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken) =>
        _screenCliService.ExecuteAsync(options, cancellationToken);
}
