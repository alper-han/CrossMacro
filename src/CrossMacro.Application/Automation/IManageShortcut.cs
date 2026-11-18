namespace CrossMacro.Application.Automation;

public interface IManageShortcut : IManageAutomationTasks<ShortcutTask>
{
    public Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> AddAsync(ShortcutTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    public Task<ShortcutTask> UpdateAsync(ShortcutTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
