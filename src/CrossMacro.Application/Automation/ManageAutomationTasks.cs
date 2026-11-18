namespace CrossMacro.Application.Automation;

/// <summary>
/// Stages task changes on detached copies and commits them under the automation mutation gate before
/// the runtime observes them. Kinds supply only their store access and per-task rules.
/// </summary>
public abstract class ManageAutomationTasks<TTask>(AutomationTaskMutationGate? mutationGate) : IDisposable
    where TTask : class
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly bool _ownsMutationGate = mutationGate is null;

    protected AutomationTaskMutationGate MutationGate { get; } = mutationGate ?? new AutomationTaskMutationGate();

    /// <summary>Lower-case kind label used in not-found messages.</summary>
    protected abstract string Kind { get; }

    protected abstract IReadOnlyList<TTask> StoredTasks { get; }

    protected abstract Task LoadStoreAsync();

    protected abstract Task CommitStoreAsync(IReadOnlyList<TTask> tasks, CancellationToken cancellationToken);

    protected abstract Guid IdOf(TTask task);

    protected abstract TTask Copy(TTask task);

    /// <summary>Re-applies the persisted-task invariants to a task about to be committed.</summary>
    protected abstract void Normalize(TTask task);

    protected abstract bool IsEnabled(TTask task);

    /// <summary>Applies the requested state; a task that cannot be enabled is left disabled by <see cref="Normalize"/>.</summary>
    protected abstract void SetEnabled(TTask task, bool enabled);

    /// <summary>Checks that the macro files the task would play may be played by the current caller.</summary>
    protected abstract void AuthorizeMacros(TTask task);

    /// <summary>Rejects tasks whose referenced files cannot be used. Runs before a task is stored or enabled.</summary>
    protected virtual void ValidateFiles(TTask task) { /* Most kinds have nothing to validate. */ }

    public Task<TaskCollectionResult<TTask>> ListAsync(CancellationToken cancellationToken = default) =>
        WithTasksAsync(tasks => new TaskCollectionResult<TTask>(tasks, MutationGate.ScopeGeneration), commit: false, expectedScopeGeneration: null, cancellationToken);

    public Task<TTask> AddAsync(TTask task, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<TTask> AddAsync(TTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration, cancellationToken);

    public Task<TTask> UpdateAsync(TTask task, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<TTask> UpdateAsync(TTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration, cancellationToken);

    public Task<TTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _ = tasks.Remove(task);
            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    public Task<TTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            SetEnabled(task, request.Enabled ?? false);
            if (IsEnabled(task))
            {
                AuthorizeMacros(task);
                ValidateFiles(task);
            }

            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    /// <summary>
    /// Binds the selected task while the profile scope is protected, then releases the gate before
    /// awaiting playback so profile operations cannot wait on themselves.
    /// </summary>
    protected async Task RunBoundAsync(TaskRequest request, Func<Guid, CancellationToken, Task> run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(run);
        var execution = await WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            AuthorizeMacros(task);
            return run(IdOf(task), cancellationToken);
        }, commit: false, request.ExpectedScopeGeneration, cancellationToken).ConfigureAwait(false);
        await execution.ConfigureAwait(false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _operationGate.Dispose();
        if (_ownsMutationGate)
        {
            MutationGate.Dispose();
        }
    }

    private Task<TTask> AddCoreAsync(TTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = Copy(task);
        return WithTasksAsync(tasks =>
        {
            if (tasks.Exists(existing => IdOf(existing) == IdOf(draft)))
            {
                throw new InvalidOperationException("A task with this id already exists.");
            }

            AuthorizeMacros(draft);
            ValidateFiles(draft);
            tasks.Add(draft);
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    private Task<TTask> UpdateCoreAsync(TTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = Copy(task);
        return WithTasksAsync(tasks =>
        {
            var existing = Find(tasks, new TaskRequest(IdOf(draft)));
            AuthorizeMacros(draft);
            ValidateFiles(draft);
            tasks[tasks.IndexOf(existing)] = draft;
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    private Task<T> WithTasksAsync<T>(Func<List<TTask>, T> operation, bool commit, long? expectedScopeGeneration, CancellationToken cancellationToken) =>
        MutationGate.RunScopedAsync(_operationGate, expectedScopeGeneration, async () =>
        {
            await LoadStoreAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var tasks = StoredTasks.Select(Copy).ToList();
            var result = operation(tasks);
            if (commit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.ForEach(Normalize);
                await CommitStoreAsync(tasks, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }, cancellationToken);

    private TTask Find(IEnumerable<TTask> tasks, TaskRequest request)
    {
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }

        return tasks.FirstOrDefault(task => IdOf(task) == id)
            ?? throw new KeyNotFoundException($"No {Kind} task found with id: {id}");
    }
}
