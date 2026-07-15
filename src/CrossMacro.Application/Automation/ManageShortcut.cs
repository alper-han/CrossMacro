
namespace CrossMacro.Application.Automation;

public sealed class ManageShortcut : IManageShortcut
{
    private readonly IShortcutService _service;
    public ManageShortcut(IShortcutService service) => _service = service ?? throw new ArgumentNullException(nameof(service));
    public async Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default) => new(await LoadAndCheckAsync(cancellationToken));
    public async Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default) => await MutateAsync(task, add: true, cancellationToken);
    public async Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default) => await MutateAsync(task, add: false, cancellationToken);
    public async Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default) => await RemoveCoreAsync(request, cancellationToken);
    public async Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default) => await SetEnabledCoreAsync(request, cancellationToken);
    public async Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await FindAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await _service.RunTaskAsync(task.Id, cancellationToken);
    }
    private async Task<ShortcutTask> MutateAsync(ShortcutTask task, bool add, CancellationToken token) { await LoadAsync(token); token.ThrowIfCancellationRequested(); if (add) _service.AddTask(task); else _service.UpdateTask(task); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); return task; }
    private async Task<ShortcutTask> RemoveCoreAsync(TaskRequest request, CancellationToken token) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); _service.RemoveTask(task.Id); try { token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { await _service.LoadAsync(); throw; } return task; }
    private async Task<ShortcutTask> SetEnabledCoreAsync(TaskRequest request, CancellationToken token) { var task = await FindAsync(request, token); token.ThrowIfCancellationRequested(); var previousEnabled = task.IsEnabled; try { _service.SetTaskEnabled(task.Id, request.Enabled ?? false); token.ThrowIfCancellationRequested(); await _service.SaveAsync(); } catch { _service.SetTaskEnabled(task.Id, previousEnabled); throw; } return task; }
    private async Task<ShortcutTask> FindAsync(TaskRequest request, CancellationToken token) { if (request.Id is not Guid id) throw new ArgumentException("A task id is required.", nameof(request)); var tasks = await LoadAsync(token); return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No shortcut task found with id: {id}"); }
    private async Task<ObservableCollection<ShortcutTask>> LoadAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); await _service.LoadAsync(); return _service.Tasks; }
    private async Task<ObservableCollection<ShortcutTask>> LoadAndCheckAsync(CancellationToken token) { var tasks = await LoadAsync(token); token.ThrowIfCancellationRequested(); return tasks; }
}
