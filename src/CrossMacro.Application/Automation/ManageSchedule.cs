
namespace CrossMacro.Application.Automation;

public sealed class ManageSchedule : IManageSchedule
{
    private readonly ISchedulerService _service;
    public ManageSchedule(ISchedulerService service) => _service = service ?? throw new ArgumentNullException(nameof(service));
    public async Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken token = default) => new(await LoadAndCheckAsync(token).ConfigureAwait(false));
    public async Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken token = default) { await LoadAsync(token); token.ThrowIfCancellationRequested(); _service.AddTask(task); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); return task; }
    public async Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken token = default) { await LoadAsync(token); token.ThrowIfCancellationRequested(); _service.UpdateTask(task); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); return task; }
    public async Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken token = default) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); _service.RemoveTask(task.Id); try { token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { await _service.LoadAsync(); throw; } return task; }
    public async Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken token = default) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); var previousEnabled = task.IsEnabled; try { _service.SetTaskEnabled(task.Id, request.Enabled ?? false); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { _service.SetTaskEnabled(task.Id, previousEnabled); throw; } return task; }
    public async Task RunAsync(TaskRequest request, CancellationToken token = default) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); await _service.RunTaskAsync(task.Id, token); }
    private async Task<ObservableCollection<ScheduledTask>> LoadAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); await _service.LoadAsync(); return _service.Tasks; }
    private async Task<ObservableCollection<ScheduledTask>> LoadAndCheckAsync(CancellationToken token) { var tasks = await LoadAsync(token); token.ThrowIfCancellationRequested(); return tasks; }
    private async Task<ScheduledTask> FindAsync(TaskRequest request, CancellationToken token) { if (request.Id is not Guid id) throw new ArgumentException("A task id is required.", nameof(request)); var tasks = await LoadAsync(token); return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No schedule task found with id: {id}"); }
}
