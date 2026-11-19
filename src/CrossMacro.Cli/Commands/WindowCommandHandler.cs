using CrossMacro.Commands.WindowManagement;

namespace CrossMacro.Cli.Commands;

public sealed class WindowCommandHandler(IWindowCommandService windowCommandService) : CliCommandHandlerBase<WindowOptions>
{
    private readonly IWindowCommandService _windowCommandService = windowCommandService ?? throw new ArgumentNullException(nameof(windowCommandService));

    protected override Task<OperationResult> ExecuteAsync(WindowOptions options, CancellationToken cancellationToken) =>
        _windowCommandService.ExecuteAsync(options, cancellationToken);
}
