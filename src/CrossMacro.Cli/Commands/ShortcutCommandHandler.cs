
namespace CrossMacro.Cli.Commands;

public sealed class ShortcutCommandHandler(IShortcutCliService shortcutCliService) : CliCommandHandlerBase<ShortcutCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
