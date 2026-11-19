namespace CrossMacro.Application.Automation;

/// <summary>
/// The command plumbing shared by the schedule, shortcut and trigger command sets: listing, id lookup,
/// adding, editing, removal, enabling and the mapping of scope failures to command results. How a command
/// changes a task is up to the kind's <see cref="TaskCommandRules{TTask,TCommand}"/>.
/// </summary>
public class TaskCommands<TTask, TCommand>(
    IManageAutomationTasks<TTask> manage,
    AutomationTaskKind<TTask> kind,
    TaskCommandRules<TTask, TCommand> rules) : ITaskCommands<TTask, TCommand>
    where TTask : class, new()
    where TCommand : class, ITaskCommand
{
    private readonly IManageAutomationTasks<TTask> _manage = manage ?? throw new ArgumentNullException(nameof(manage));
    private readonly TaskCommandRules<TTask, TCommand> _rules = rules ?? throw new ArgumentNullException(nameof(rules));

    protected AutomationTaskKind<TTask> Kind { get; } = kind ?? throw new ArgumentNullException(nameof(kind));

    public Task<TaskCommandResult<TTask>> ListAsync(CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var result = await _manage.ListAsync(cancellationToken).ConfigureAwait(false);
            return new TaskCommandResult<TTask>(Success: true, $"Loaded {result.Tasks.Count} {Kind.Name} task(s).", [], Tasks: result.Tasks);
        });

    public Task<TaskCommandResult<TTask>> ExecuteAsync(TCommand options, CancellationToken cancellationToken) =>
        GuardAsync(() => ExecuteCoreAsync(options, cancellationToken));

    /// <summary>Runs <paramref name="operation"/> and turns a lost or unavailable profile scope into a failed result.</summary>
    protected async Task<TaskCommandResult<TTask>> GuardAsync(Func<Task<TaskCommandResult<TTask>>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<TTask>(ex.Message);
        }
    }

    protected async Task<(TTask? Task, TaskCommandResult<TTask>? Result, long ScopeGeneration)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id))
        {
            return (null, TaskCommandResult.Fail<TTask>($"Invalid {Kind.Name} task id format.", [$"Task id is not a valid GUID: {taskId}"]), 0);
        }

        var tasks = await _manage.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = tasks.Tasks.FirstOrDefault(candidate => Kind.IdOf(candidate) == id);
        return task is null
            ? (null, TaskCommandResult.Fail<TTask>($"{Kind.Title} task not found.", [$"No {Kind.Name} task found with id: {taskId}"]), tasks.ScopeGeneration)
            : (task, null, tasks.ScopeGeneration);
    }

    private async Task<TaskCommandResult<TTask>> ExecuteCoreAsync(TCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var taskId = command.TaskId ?? string.Empty;
        try
        {
            return command.Action switch
            {
                TaskCommandAction.Add => await AddAsync(command, cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Edit => await UpdateAsync(taskId, task => _rules.ApplyEdit(task, command), command.Enabled, "updated", cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Remove => await RemoveAsync(taskId, cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Enable => await SetEnabledAsync(taskId, enabled: true, cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Disable => await SetEnabledAsync(taskId, enabled: false, cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Bind when _rules.Bind is { } bind => await UpdateAsync(taskId, task => bind(task, command), enabled: null, "bound", cancellationToken).ConfigureAwait(false),
                TaskCommandAction.Next when _rules.DescribeNext is { } describeNext => await NextAsync(taskId, describeNext, cancellationToken).ConfigureAwait(false),
                _ => TaskCommandResult.Fail<TTask>($"Unknown {Kind.Name} action."),
            };
        }
        catch (FileNotFoundException ex)
        {
            return TaskCommandResult.Fail<TTask>($"Invalid {Kind.Name} macros.", [ex.Message]);
        }
    }

    private async Task<TaskCommandResult<TTask>> AddAsync(TCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = new TTask();
        if (_rules.ApplyAdd(task, command) is { } failure)
        {
            return failure;
        }

        if (command.Enabled is { } enabled && !Kind.TrySetEnabled(task, enabled))
        {
            return CannotEnable();
        }

        var scope = await _manage.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        task = await _manage.AddAsync(task, scope.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok($"{Kind.Title} task added: {Kind.NameOf(task)}.", task);
    }

    private async Task<TaskCommandResult<TTask>> UpdateAsync(
        string taskId,
        Func<TTask, TaskCommandResult<TTask>?> apply,
        bool? enabled,
        string verb,
        CancellationToken cancellationToken)
    {
        var (found, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var task = Kind.Copy(found!);
        if (apply(task) is { } applyFailure)
        {
            return applyFailure;
        }

        if (enabled is { } requested && !Kind.TrySetEnabled(task, requested))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manage.UpdateAsync(task, scopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok($"{Kind.Title} task {verb}: {Kind.NameOf(task)}.", task);
    }

    private async Task<TaskCommandResult<TTask>> NextAsync(string taskId, Func<TTask, string> describeNext, CancellationToken cancellationToken)
    {
        var (task, failure, _) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        return failure ?? TaskCommandResult.Ok(describeNext(task!), task);
    }

    private async Task<TaskCommandResult<TTask>> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var (task, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manage.RemoveAsync(new TaskRequest(Kind.IdOf(task!), ExpectedScopeGeneration: scopeGeneration), cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok($"{Kind.Title} task removed: {Kind.NameOf(task!)}.", task);
    }

    private async Task<TaskCommandResult<TTask>> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var (task, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        if (enabled && !Kind.CanBeEnabled(task!))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var committed = await _manage.SetEnabledAsync(new TaskRequest(Kind.IdOf(task!), enabled, scopeGeneration), cancellationToken).ConfigureAwait(false);
        var verb = enabled ? "enabled" : "disabled";
        return TaskCommandResult.Ok($"{Kind.Title} task {verb}: {Kind.NameOf(committed)}.", committed);
    }

    private TaskCommandResult<TTask> CannotEnable() =>
        TaskCommandResult.Fail<TTask>($"{Kind.Title} task cannot be enabled.", [Kind.CannotEnableReason]);
}
