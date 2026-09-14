namespace CrossMacro.Cli.Commands;

public sealed class ShortcutCommandHandler(IShortcutCommands commands) : CliCommandHandlerBase<ShortcutCliOptions>
{
    private readonly IShortcutCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken).ConfigureAwait(false));
    }
}
