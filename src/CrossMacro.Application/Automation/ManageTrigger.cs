
namespace CrossMacro.Application.Automation;

public sealed class ManageTrigger(ITriggerTaskOperations operations, ITriggerTaskStore store) : IManageTrigger
{
    private readonly ITriggerTaskOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly ITriggerTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default) =>
        new(await LoadAndCheckAsync(cancellationToken).ConfigureAwait(false));

    public async Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default)
    {
        _ = await LoadAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _operations.AddTask(task);
        cancellationToken.ThrowIfCancellationRequested();
        await _store.SaveAsync().ConfigureAwait(false);
        return task;
    }

    public async Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default)
    {
        _ = await LoadAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _operations.UpdateTask(task);
        cancellationToken.ThrowIfCancellationRequested();
        await _store.SaveAsync().ConfigureAwait(false);
        return task;
    }
    public async Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _operations.RemoveTask(task.Id);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await _store.LoadAsync().ConfigureAwait(false);
            throw;
        }

        return task;
    }
    public async Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var previousEnabled = task.IsEnabled;
        try
        {
            _operations.SetTaskEnabled(task.Id, request.Enabled ?? false);
            cancellationToken.ThrowIfCancellationRequested();
            await _store.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _operations.SetTaskEnabled(task.Id, previousEnabled);
            throw;
        }

        return task;
    }

    private async Task<IReadOnlyList<TriggerTask>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _store.LoadAsync().ConfigureAwait(false);
        return _store.Tasks;
    }

    private async Task<IReadOnlyList<TriggerTask>> LoadAndCheckAsync(CancellationToken cancellationToken)
    {
        var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return tasks;
    }

    private async Task<TriggerTask> FindAsync(TaskRequest request, CancellationToken cancellationToken)
    {
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }

        var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return tasks.FirstOrDefault(task => task.Id == id)
            ?? throw new KeyNotFoundException($"No trigger task found with id: {id}");
    }
}
