
namespace CrossMacro.Application.Automation;

public sealed class ManageSchedule(ISchedulerService service) : IManageSchedule
{
    private readonly ISchedulerService _service = service ?? throw new ArgumentNullException(nameof(service));

    public async Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken = default) => new(await LoadAndCheckAsync(cancellationToken).ConfigureAwait(false));
    public async Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default) { _ = await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.AddTask(task); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); return task; }
    public async Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default) { _ = await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.UpdateTask(task); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); return task; }
    public async Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); _service.RemoveTask(task.Id); try { cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); } catch (Exception ex) when (ex is not OutOfMemoryException) { await _service.LoadAsync().ConfigureAwait(false); throw; }
        return task;
    }
    public async Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); var previousEnabled = task.IsEnabled; try { _service.SetTaskEnabled(task.Id, request.Enabled ?? false); cancellationToken.ThrowIfCancellationRequested(); await _service.SaveAsync().ConfigureAwait(false); } catch (Exception ex) when (ex is not OutOfMemoryException) { _service.SetTaskEnabled(task.Id, previousEnabled); throw; }
        return task;
    }
    public async Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await FindAsync(request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); await _service.RunTaskAsync(task.Id, cancellationToken).ConfigureAwait(false);
    }
    private async Task<ObservableCollection<ScheduledTask>> LoadAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); await _service.LoadAsync().ConfigureAwait(false); return _service.Tasks; }
    private async Task<ObservableCollection<ScheduledTask>> LoadAndCheckAsync(CancellationToken cancellationToken) { var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return tasks; }
    private async Task<ScheduledTask> FindAsync(TaskRequest request, CancellationToken cancellationToken) { if (request.Id is not Guid id) { throw new ArgumentException("A task id is required.", nameof(request)); } var tasks = await LoadAsync(cancellationToken).ConfigureAwait(false); return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No schedule task found with id: {id}"); }
}
