
namespace CrossMacro.Cli.Commands;

public sealed class WindowCommandHandler(IWindowCliService windowCliService) : CliCommandHandlerBase<WindowCliOptions>
{
    private readonly IWindowCliService _windowCliService = windowCliService;

    protected override Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken) =>
        _windowCliService.ExecuteAsync(options, cancellationToken);
}
