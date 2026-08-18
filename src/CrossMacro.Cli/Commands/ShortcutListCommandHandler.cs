
namespace CrossMacro.Cli.Commands;

public sealed class ShortcutListCommandHandler(IShortcutCliService shortcutCliService) : CliCommandHandlerBase<ShortcutListCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutListCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.ListAsync(cancellationToken).ConfigureAwait(false);
    }
}
