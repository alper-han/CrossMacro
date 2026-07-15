
namespace CrossMacro.Application.Automation;

public sealed class ManageTrigger : IManageTrigger
{
    private readonly ITriggerService _service;
    public ManageTrigger(ITriggerService service) => _service = service ?? throw new ArgumentNullException(nameof(service));
    public async Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default) => new(await LoadAndCheckAsync(cancellationToken).ConfigureAwait(false));
    public async Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default) { await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.AddTask(task); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); return task; }
    public async Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default) { await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.UpdateTask(task); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); return task; }
    public async Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.RemoveTask(task.Id); try { cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); } catch { await _service.LoadAsync().ConfigureAwait(false); throw; } return task;
    }
    public async Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); var previousEnabled = task.IsEnabled; try { _service.SetTaskEnabled(task.Id, request.Enabled ?? false); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); } catch { _service.SetTaskEnabled(task.Id, previousEnabled); throw; } return task;
    }
    private async Task<ObservableCollection<TriggerTask>> LoadAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); await _service.LoadAsync().ConfigureAwait(false); return _service.Tasks; }
    private async Task<ObservableCollection<TriggerTask>> LoadAndCheckAsync(CancellationToken cancellationToken) { var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return tasks; }
    private async Task<TriggerTask> FindAsync(TaskRequest request, CancellationToken cancellationToken) { if (request.Id is not Guid id) { throw new ArgumentException("A task id is required.", nameof(request)); } var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false); return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No trigger task found with id: {id}"); }
}
