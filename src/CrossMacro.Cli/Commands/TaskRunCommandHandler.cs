namespace CrossMacro.Cli.Commands;

/// <summary>Runs one task of a kind that can be run on demand.</summary>
public sealed class TaskRunCommandHandler<TOptions, TTask, TCommand> : CliCommandHandlerBase<TOptions>
    where TOptions : CliCommandOptions
    where TTask : class
    where TCommand : ITaskCommand
{
    private readonly IRunnableTaskCommands<TTask, TCommand> _commands;
    private readonly Func<TOptions, string> _taskId;
    private readonly TaskCliResultMapper<TTask> _results;

    internal TaskRunCommandHandler(IRunnableTaskCommands<TTask, TCommand> commands, Func<TOptions, string> taskId, TaskCliResultMapper<TTask> results)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken) =>
        _results.FromApplication(await _commands.RunAsync(_taskId(options), cancellationToken).ConfigureAwait(false));
}
