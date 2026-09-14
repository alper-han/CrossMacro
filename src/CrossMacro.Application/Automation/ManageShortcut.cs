namespace CrossMacro.Application.Automation;

/// <summary>Stages task changes and commits them before the runtime observes them.</summary>
public sealed class ManageShortcut(IShortcutTaskOperations operations, IShortcutTaskStore store, AutomationTaskMutationGate? mutationGate = null) : IManageShortcut, IDisposable
{
    private readonly IShortcutTaskOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly IShortcutTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly AutomationTaskMutationGate _mutationGate = mutationGate ?? new();
    private readonly bool _ownsMutationGate = mutationGate is null;

    public Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default) =>
        WithTasksAsync(tasks => new TaskCollectionResult<ShortcutTask>(tasks, _mutationGate.ScopeGeneration), commit: false, expectedScopeGeneration: null, cancellationToken);

    public Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<ShortcutTask> AddAsync(ShortcutTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        AddCoreAsync(task, expectedScopeGeneration, cancellationToken);

    private Task<ShortcutTask> AddCoreAsync(ShortcutTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = AutomationTaskSnapshots.Copy(task);
        return WithTasksAsync(tasks =>
        {
            if (tasks.Exists(existing => existing.Id == draft.Id))
            {
                throw new InvalidOperationException("A task with this id already exists.");
            }
            _mutationGate.AuthorizeMacroPath(draft.MacroFilePath);
            tasks.Add(draft);
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    public Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration: null, cancellationToken);

    public Task<ShortcutTask> UpdateAsync(ShortcutTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(task, expectedScopeGeneration, cancellationToken);

    private Task<ShortcutTask> UpdateCoreAsync(ShortcutTask task, long? expectedScopeGeneration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        var draft = AutomationTaskSnapshots.Copy(task);
        return WithTasksAsync(tasks =>
        {
            var existing = Find(tasks, new TaskRequest(draft.Id));
            _mutationGate.AuthorizeMacroPath(draft.MacroFilePath);
            tasks[tasks.IndexOf(existing)] = draft;
            return draft;
        }, commit: true, expectedScopeGeneration, cancellationToken);
    }

    public Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _ = tasks.Remove(task);
            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    public Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _ = task.TrySetEnabled(request.Enabled ?? false);
            if (task.IsEnabled)
            {
                _mutationGate.AuthorizeMacroPath(task.MacroFilePath);
            }
            return task;
        }, commit: true, request.ExpectedScopeGeneration, cancellationToken);
    }

    public async Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Bind the selected runtime task while the profile scope is protected, then release
        // the gate before awaiting playback so profile operations cannot wait on themselves.
        var execution = await WithTasksAsync(tasks =>
        {
            var task = Find(tasks, request);
            _mutationGate.AuthorizeMacroPath(task.MacroFilePath);
            return _operations.RunTaskAsync(task.Id, cancellationToken);
        }, commit: false, request.ExpectedScopeGeneration, cancellationToken).ConfigureAwait(false);
        await execution.ConfigureAwait(false);
    }

    private Task<T> WithTasksAsync<T>(Func<List<ShortcutTask>, T> operation, bool commit, long? expectedScopeGeneration, CancellationToken cancellationToken) =>
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

    private static ShortcutTask Find(IEnumerable<ShortcutTask> tasks, TaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }
        return tasks.FirstOrDefault(task => task.Id == id)
            ?? throw new KeyNotFoundException($"No shortcut task found with id: {id}");
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
