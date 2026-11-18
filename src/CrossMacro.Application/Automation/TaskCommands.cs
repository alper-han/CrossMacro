namespace CrossMacro.Application.Automation;

/// <summary>
/// Shared command plumbing of the schedule, shortcut and trigger command sets: listing, id lookup,
/// removal, enabling and the mapping of scope failures to command results.
/// </summary>
public abstract class TaskCommands<TTask>(IManageAutomationTasks<TTask> manage)
    where TTask : class
{
    /// <summary>Lower-case kind label used in messages, for example "schedule".</summary>
    protected abstract string Kind { get; }

    protected abstract Guid IdOf(TTask task);

    protected abstract string NameOf(TTask task);

    protected abstract bool CanBeEnabled(TTask task);

    /// <summary>The reason shown when a task that cannot be enabled is asked to be.</summary>
    protected abstract string CannotEnableReason { get; }

    public Task<TaskCommandResult<TTask>> ListAsync(CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var result = await manage.ListAsync(cancellationToken).ConfigureAwait(false);
            return new TaskCommandResult<TTask>(Success: true, $"Loaded {result.Tasks.Count} {Kind} task(s).", [], Tasks: result.Tasks);
        });

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

    protected async Task<TaskCommandResult<TTask>> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var (task, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await manage.RemoveAsync(new TaskRequest(IdOf(task!), ExpectedScopeGeneration: scopeGeneration), cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok($"{Title} task removed: {NameOf(task!)}.", task);
    }

    protected async Task<TaskCommandResult<TTask>> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var (task, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        if (enabled && !CanBeEnabled(task!))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var committed = await manage.SetEnabledAsync(new TaskRequest(IdOf(task!), enabled, scopeGeneration), cancellationToken).ConfigureAwait(false);
        var verb = enabled ? "enabled" : "disabled";
        return TaskCommandResult.Ok($"{Title} task {verb}: {NameOf(committed)}.", committed);
    }

    protected TaskCommandResult<TTask> CannotEnable() =>
        TaskCommandResult.Fail<TTask>($"{Title} task cannot be enabled.", [CannotEnableReason]);

    protected async Task<(TTask? Task, TaskCommandResult<TTask>? Result, long ScopeGeneration)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id))
        {
            return (null, TaskCommandResult.Fail<TTask>($"Invalid {Kind} task id format.", [$"Task id is not a valid GUID: {taskId}"]), 0);
        }

        var tasks = await manage.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = tasks.Tasks.FirstOrDefault(candidate => IdOf(candidate) == id);
        return task is null
            ? (null, TaskCommandResult.Fail<TTask>($"{Title} task not found.", [$"No {Kind} task found with id: {taskId}"]), tasks.ScopeGeneration)
            : (task, null, tasks.ScopeGeneration);
    }

    private string Title => field ??= string.Concat(char.ToUpperInvariant(Kind[0]).ToString(), Kind.AsSpan(1));
}
