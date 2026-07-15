
namespace CrossMacro.Cli.Commands;

public sealed class ShortcutCommandHandler : CliCommandHandlerBase<ShortcutCliOptions>
{
    private readonly IShortcutCliService _shortcutCliService;

    public ShortcutCommandHandler(IShortcutCliService shortcutCliService)
    {
        _shortcutCliService = shortcutCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        return _shortcutCliService.ExecuteAsync(options, cancellationToken);
    }
}
