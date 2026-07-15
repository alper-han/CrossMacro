
namespace CrossMacro.Application.Automation;

public sealed class ManageTrigger : IManageTrigger
{
    private readonly ITriggerService _service;
    public ManageTrigger(ITriggerService service) => _service = service ?? throw new ArgumentNullException(nameof(service));
    public async Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken token = default) => new(await LoadAndCheckAsync(token));
    public async Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken token = default) { await LoadAsync(token); token.ThrowIfCancellationRequested(); _service.AddTask(task); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); return task; }
    public async Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken token = default) { await LoadAsync(token); token.ThrowIfCancellationRequested(); _service.UpdateTask(task); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); return task; }
    public async Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken token = default) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); _service.RemoveTask(task.Id); try { token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { await _service.LoadAsync(); throw; } return task; }
    public async Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken token = default) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); var previousEnabled = task.IsEnabled; try { _service.SetTaskEnabled(task.Id, request.Enabled ?? false); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { _service.SetTaskEnabled(task.Id, previousEnabled); throw; } return task; }
    private async Task<ObservableCollection<TriggerTask>> LoadAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); await _service.LoadAsync(); return _service.Tasks; }
    private async Task<ObservableCollection<TriggerTask>> LoadAndCheckAsync(CancellationToken token) { var tasks = await LoadAsync(token); token.ThrowIfCancellationRequested(); return tasks; }
    private async Task<TriggerTask> FindAsync(TaskRequest request, CancellationToken token) { if (request.Id is not Guid id) throw new ArgumentException("A task id is required.", nameof(request)); var tasks = await LoadAsync(token); return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No trigger task found with id: {id}"); }
}
