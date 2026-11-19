namespace CrossMacro.Cli.Commands;

/// <summary>Lists the tasks of one task kind.</summary>
public sealed class TaskListCommandHandler<TOptions, TTask, TCommand> : CliCommandHandlerBase<TOptions>
    where TOptions : CommandOptions
    where TTask : class
    where TCommand : ITaskCommand
{
    private readonly ITaskCommands<TTask, TCommand> _commands;
    private readonly TaskCliResultMapper<TTask> _results;

    internal TaskListCommandHandler(ITaskCommands<TTask, TCommand> commands, TaskCliResultMapper<TTask> results)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    protected override async Task<OperationResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken) =>
        _results.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));
}
