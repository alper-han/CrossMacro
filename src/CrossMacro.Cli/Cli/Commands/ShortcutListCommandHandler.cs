using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ShortcutListCommandHandler : CliCommandHandlerBase<ShortcutListCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService;

    public ShortcutListCommandHandler(IShortcutCliService shortcutCliService)
    {
        _shortcutCliService = shortcutCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutListCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.ListAsync(cancellationToken);
    }
}
