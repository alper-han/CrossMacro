using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class WindowCommandHandler : CliCommandHandlerBase<WindowCliOptions>
{
    private readonly IWindowCliService _windowCliService;

    public WindowCommandHandler(IWindowCliService windowCliService)
    {
        _windowCliService = windowCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken) =>
        _windowCliService.ExecuteAsync(options, cancellationToken);
}
