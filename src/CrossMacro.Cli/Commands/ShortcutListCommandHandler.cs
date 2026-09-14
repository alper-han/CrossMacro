namespace CrossMacro.Cli.Commands;

public sealed class ShortcutListCommandHandler(IShortcutCommands commands) : CliCommandHandlerBase<ShortcutListCliOptions>
{
    private readonly IShortcutCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutListCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));
    }
}
