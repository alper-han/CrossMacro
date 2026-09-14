namespace CrossMacro.Cli.Commands;

public sealed class ShortcutRunCommandHandler(IShortcutCommands commands) : CliCommandHandlerBase<ShortcutRunCliOptions>
{
    private readonly IShortcutCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutRunCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.RunAsync(options.TaskId, cancellationToken).ConfigureAwait(false));
    }
}
