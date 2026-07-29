
namespace CrossMacro.Cli.Commands;

public sealed class ShortcutRunCommandHandler(IShortcutCliService shortcutCliService) : CliCommandHandlerBase<ShortcutRunCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutRunCliOptions options, CancellationToken cancellationToken)
    {
        return await _shortcutCliService.RunAsync(options.TaskId, cancellationToken).ConfigureAwait(false);
    }
}
