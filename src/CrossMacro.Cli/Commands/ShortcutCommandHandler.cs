
namespace CrossMacro.Cli.Commands;

public sealed class ShortcutCommandHandler(IShortcutCliService shortcutCliService) : CliCommandHandlerBase<ShortcutCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService ?? throw new ArgumentNullException(nameof(shortcutCliService));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
