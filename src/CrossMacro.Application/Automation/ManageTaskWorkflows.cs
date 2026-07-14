using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Application.Automation;

public sealed record TaskRequest(Guid? Id = null, bool? Enabled = null);

public sealed record TaskCollectionResult<T>(IReadOnlyList<T> Tasks);

public interface IManageShortcut
{
    Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default);
    Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}

public interface IManageSchedule
{
    Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}

public interface IManageTrigger
{
    Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default);
    Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default);
    Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
}

public sealed class ManageShortcut : IManageShortcut
{
    private readonly IShortcutService _service;
    public ManageShortcut(IShortcutService service) => _service = service ?? throw new ArgumentNullException(nameof(service));
    public async Task<TaskCollectionResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken = default) =>
        new(await LoadAndCheckAsync(cancellationToken));
    public async Task<ShortcutTask> AddAsync(ShortcutTask task, CancellationToken cancellationToken = default) =>
        await MutateAsync(task, add: true, cancellationToken);
    public async Task<ShortcutTask> UpdateAsync(ShortcutTask task, CancellationToken cancellationToken = default) =>
        await MutateAsync(task, add: false, cancellationToken);
    public async Task<ShortcutTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default) =>
        await RemoveCoreAsync(request, cancellationToken);
    public async Task<ShortcutTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default) =>
        await SetEnabledCoreAsync(request, cancellationToken);
    public async Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await FindAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await _service.RunTaskAsync(task.Id, cancellationToken);
    }
    private async Task<ShortcutTask> MutateAsync(ShortcutTask task, bool add, CancellationToken token)
    {
        await LoadAsync(token);
        token.ThrowIfCancellationRequested();
        if (add) _service.AddTask(task); else _service.UpdateTask(task);
        token.ThrowIfCancellationRequested();
        await _service.SaveAsync();
        return task;
    }
    private async Task<ShortcutTask> RemoveCoreAsync(TaskRequest request, CancellationToken token)
    {
        var task = await FindAsync(request, token);
        token.ThrowIfCancellationRequested();
        _service.RemoveTask(task.Id);
        try
        {
            token.ThrowIfCancellationRequested();
            await _service.SaveAsync();
        }
        catch
        {
            await _service.LoadAsync();
            throw;
        }
        return task;
    }
    private async Task<ShortcutTask> SetEnabledCoreAsync(TaskRequest request, CancellationToken token)
    {
        var task = await FindAsync(request, token);
        token.ThrowIfCancellationRequested();
        var previousEnabled = task.IsEnabled;
        try
        {
            _service.SetTaskEnabled(task.Id, request.Enabled ?? false);
            token.ThrowIfCancellationRequested();
            await _service.SaveAsync();
        }
        catch
        {
            _service.SetTaskEnabled(task.Id, previousEnabled);
            throw;
        }
        return task;
    }
    private async Task<ShortcutTask> FindAsync(TaskRequest request, CancellationToken token)
    {
        if (request.Id is not Guid id) throw new ArgumentException("A task id is required.", nameof(request));
        var tasks = await LoadAsync(token);
        return tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"No shortcut task found with id: {id}");
    }
    private async Task<ObservableCollection<ShortcutTask>> LoadAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); await _service.LoadAsync(); return _service.Tasks; }
    private async Task<ObservableCollection<ShortcutTask>> LoadAndCheckAsync(CancellationToken token) { var tasks = await LoadAsync(token); token.ThrowIfCancellationRequested(); return tasks; }
}

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
