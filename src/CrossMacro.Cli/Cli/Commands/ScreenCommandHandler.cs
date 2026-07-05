using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ScreenCommandHandler : CliCommandHandlerBase<ScreenCliOptions>
{
    private readonly IScreenCliService _screenCliService;

    public ScreenCommandHandler(IScreenCliService screenCliService)
    {
        _screenCliService = screenCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken) =>
        _screenCliService.ExecuteAsync(options, cancellationToken);
}
