
namespace CrossMacro.Application.Automation;

public interface IManageShortcut
{
    public Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default);
    public Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
