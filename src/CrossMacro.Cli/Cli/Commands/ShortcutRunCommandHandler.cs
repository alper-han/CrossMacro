using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ShortcutRunCommandHandler : CliCommandHandlerBase<ShortcutRunCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService;

    public ShortcutRunCommandHandler(IShortcutCliService shortcutCliService)
    {
        _shortcutCliService = shortcutCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutRunCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.RunAsync(options.TaskId, cancellationToken);
    }
}
