namespace CrossMacro.Application.Automation;

/// <summary>
/// Stages task changes on detached copies and commits them under the automation mutation gate before
/// the runtime observes them. <see cref="AutomationTaskKind{TTask}"/> supplies the per-task rules.
/// </summary>
public class ManageAutomationTasks<TTask>(
    AutomationTaskKind<TTask> kind,
    IAutomationTaskStore<TTask> store,
    AutomationTaskMutationGate? mutationGate = null) : IManageAutomationTasks<TTask>, IDisposable
    where TTask : class
{
    private readonly AutomationTaskKind<TTask> _kind = kind ?? throw new ArgumentNullException(nameof(kind));
    private readonly IAutomationTaskStore<TTask> _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly bool _ownsMutationGate = mutationGate is null;

    protected AutomationTaskMutationGate MutationGate { get; } = mutationGate ?? new AutomationTaskMutationGate();

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
            _ = _kind.TrySetEnabled(task, request.Enabled ?? false);
            if (_kind.IsEnabled(task))
            {
                AuthorizeMacros(task);
                _kind.ValidateFiles(task);
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
            return run(_kind.IdOf(task), cancellationToken);
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
        var draft = _kind.Copy(task);
        return WithTasksAsync(tasks =>
        {
            if (tasks.Exists(existing => _kind.IdOf(existing) == _kind.IdOf(draft)))
            {
                throw new InvalidOperationException("A task with this id already exists.");
            }

            AuthorizeMacros(draft);
            _kind.ValidateFiles(draft);
            tasks.Add(draft);
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    private Task<TTask> UpdateCoreAsync(TTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = _kind.Copy(task);
        return WithTasksAsync(tasks =>
        {
            var existing = Find(tasks, new TaskRequest(_kind.IdOf(draft)));
            AuthorizeMacros(draft);
            _kind.ValidateFiles(draft);
            tasks[tasks.IndexOf(existing)] = draft;
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    private Task<T> WithTasksAsync<T>(Func<List<TTask>, T> operation, bool commit, long? expectedScopeGeneration, CancellationToken cancellationToken) =>
        MutationGate.RunScopedAsync(_operationGate, expectedScopeGeneration, async () =>
        {
            await _store.LoadAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var tasks = _store.Tasks.Select(_kind.Copy).ToList();
            var result = operation(tasks);
            if (commit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.ForEach(_kind.Normalize);
                await _store.CommitAsync(tasks, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }, cancellationToken);

    private void AuthorizeMacros(TTask task)
    {
        foreach (var macroPath in _kind.MacroPaths(task))
        {
            MutationGate.AuthorizeMacroPath(macroPath);
        }
    }

    private TTask Find(IEnumerable<TTask> tasks, TaskRequest request)
    {
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }

        return tasks.FirstOrDefault(task => _kind.IdOf(task) == id)
            ?? throw new KeyNotFoundException($"No {_kind.Name} task found with id: {id}");
    }
}
