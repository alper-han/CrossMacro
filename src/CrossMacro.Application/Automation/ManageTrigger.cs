namespace CrossMacro.Application.Automation;

/// <summary>Stages task changes and commits them before the runtime observes them.</summary>
public sealed class ManageTrigger : IManageTrigger, IDisposable
{
    private readonly ITriggerTaskStore _store;
    private readonly AutomationTaskMutationGate _mutationGate;
    private readonly bool _ownsMutationGate;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public ManageTrigger(ITriggerTaskOperations operations, ITriggerTaskStore store, AutomationTaskMutationGate? mutationGate = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mutationGate = mutationGate ?? new AutomationTaskMutationGate();
        _ownsMutationGate = mutationGate is null;
    }

    public Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default) =>
        WithTasksAsync(tasks => new TaskCollectionResult<TriggerTask>(tasks, _mutationGate.ScopeGeneration), commit: false, expectedScopeGeneration: null, cancellationToken);

    public Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<TriggerTask> AddAsync(TriggerTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration, cancellationToken);

    private Task<TriggerTask> AddCoreAsync(TriggerTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = AutomationTaskSnapshots.Copy(task);
        return WithTasksAsync(tasks =>
        {
            if (tasks.Exists(existing => existing.Id == draft.Id))
            {
                throw new InvalidOperationException("A task with this id already exists.");
            }
            _mutationGate.AuthorizeMacroPath(draft.Action is TriggerOperation.RunMacro ? draft.MacroFilePath : null);
            tasks.Add(draft);
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    public Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<TriggerTask> UpdateAsync(TriggerTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration, cancellationToken);

    private Task<TriggerTask> UpdateCoreAsync(TriggerTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = AutomationTaskSnapshots.Copy(task);
        return WithTasksAsync(tasks =>
        {
            var existing = Find(tasks, new TaskRequest(draft.Id));
            _mutationGate.AuthorizeMacroPath(draft.Action is TriggerOperation.RunMacro ? draft.MacroFilePath : null);
            tasks[tasks.IndexOf(existing)] = draft;
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    public Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _ = tasks.Remove(task);
            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    public Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _ = task.TrySetEnabled(request.Enabled ?? false);
            if (task.IsEnabled)
            {
                _mutationGate.AuthorizeMacroPath(task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null);
            }
            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    private Task<T> WithTasksAsync<T>(Func<List<TriggerTask>, T> operation, bool commit, long? expectedScopeGeneration, CancellationToken cancellationToken) =>
        _mutationGate.RunScopedAsync(_operationGate, expectedScopeGeneration, async () =>
        {
            await _store.LoadAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var tasks = _store.Tasks.Select(AutomationTaskSnapshots.Copy).ToList();
            var result = operation(tasks);
            if (commit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AutomationTaskWriteValidator.NormalizeForCommit(tasks);
                await _store.CommitAsync(tasks, cancellationToken).ConfigureAwait(false);
            }
            return result;
        }, cancellationToken);

    private static TriggerTask Find(IEnumerable<TriggerTask> tasks, TaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }
        return tasks.FirstOrDefault(task => task.Id == id)
            ?? throw new KeyNotFoundException($"No trigger task found with id: {id}");
    }

    public void Dispose()
    {
        _operationGate.Dispose();
        if (_ownsMutationGate)
        {
            _mutationGate.Dispose();
        }
    }
}
