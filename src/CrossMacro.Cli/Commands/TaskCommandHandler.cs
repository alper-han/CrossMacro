namespace CrossMacro.Cli.Commands;

/// <summary>Runs the add, edit, remove, enable, disable and kind-specific subcommands of one task kind.</summary>
public sealed class TaskCommandHandler<TOptions, TTask, TCommand> : CliCommandHandlerBase<TOptions>
    where TOptions : CommandOptions
    where TTask : class
    where TCommand : ITaskCommand
{
    private readonly ITaskCommands<TTask, TCommand> _commands;
    private readonly Func<TOptions, TCommand> _toCommand;
    private readonly TaskCliResultMapper<TTask> _results;

    internal TaskCommandHandler(ITaskCommands<TTask, TCommand> commands, Func<TOptions, TCommand> toCommand, TaskCliResultMapper<TTask> results)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _toCommand = toCommand ?? throw new ArgumentNullException(nameof(toCommand));
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    protected override async Task<OperationResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken) =>
        _results.FromApplication(await _commands.ExecuteAsync(_toCommand(options), cancellationToken).ConfigureAwait(false));
}
