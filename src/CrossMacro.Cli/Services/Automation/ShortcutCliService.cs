namespace CrossMacro.Cli.Services.Automation;

/// <summary>Maps CLI syntax and output around the shared application task commands.</summary>
public sealed class ShortcutCliService(IShortcutCommands commands) : IShortcutCliService
{
    private readonly IShortcutCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    public ShortcutCliService(IManageShortcut workflow) : this(new ShortcutCommands(workflow)) { }


    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) =>
        TaskCliResultMapper.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));

    public async Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken) =>
        TaskCliResultMapper.FromApplication(await _commands.RunAsync(taskId, cancellationToken).ConfigureAwait(false));

    public async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return TaskCliResultMapper.FromApplication(await _commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken).ConfigureAwait(false));
    }
}
