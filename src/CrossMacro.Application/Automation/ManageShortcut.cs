
namespace CrossMacro.Application.Automation;

public sealed class ManageShortcut(IShortcutTaskOperations operations, IShortcutTaskStore store) : IManageShortcut
{
    private readonly IShortcutTaskOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly IShortcutTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default) =>
        new(await LoadAndCheckAsync(cancellationToken).ConfigureAwait(false));
    public async Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default) => await MutateAsync(task, add: true, cancellationToken).ConfigureAwait(false);
    public async Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default) => await MutateAsync(task, add: false, cancellationToken).ConfigureAwait(false);
    public async Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default) => await RemoveCoreAsync(request, cancellationToken).ConfigureAwait(false);
    public async Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await SetEnabledCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }
    public async Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await _operations.RunTaskAsync(task.Id, cancellationToken).ConfigureAwait(false);
    }
    private async Task<ShortcutTask> MutateAsync(ShortcutTask task, bool add, CancellationToken token)
    {
        _ = await LoadAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (add)
        {
            _operations.AddTask(task);
        }
        else
        {
            _operations.UpdateTask(task);
        }
        token.ThrowIfCancellationRequested();
        await _store.SaveAsync().ConfigureAwait(false);
        return task;
    }

    private async Task<ShortcutTask> RemoveCoreAsync(TaskRequest request, CancellationToken token)
    {
        var task = await FindAsync(request, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        _operations.RemoveTask(task.Id);
        try
        {
            token.ThrowIfCancellationRequested();
            await _store.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await _store.LoadAsync().ConfigureAwait(false);
            throw;
        }

        return task;
    }

    private async Task<ShortcutTask> SetEnabledCoreAsync(TaskRequest request, CancellationToken token)
    {
        var task = await FindAsync(request, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var previousEnabled = task.IsEnabled;
        try
        {
            _operations.SetTaskEnabled(task.Id, request.Enabled ?? false);
            token.ThrowIfCancellationRequested();
            await _store.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _operations.SetTaskEnabled(task.Id, previousEnabled);
            throw;
        }

        return task;
    }

    private async Task<ShortcutTask> FindAsync(TaskRequest request, CancellationToken token)
    {
        if (request.Id is not Guid id)
        {
            throw new ArgumentException("A task id is required.", nameof(request));
        }

        var tasks = await LoadAsync(token).ConfigureAwait(false);
        return tasks.FirstOrDefault(task => task.Id == id)
            ?? throw new KeyNotFoundException($"No shortcut task found with id: {id}");
    }

    private async Task<IReadOnlyList<ShortcutTask>> LoadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await _store.LoadAsync().ConfigureAwait(false);
        return _store.Tasks;
    }

    private async Task<IReadOnlyList<ShortcutTask>> LoadAndCheckAsync(CancellationToken token)
    {
        var tasks = await LoadAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return tasks;
    }
}
