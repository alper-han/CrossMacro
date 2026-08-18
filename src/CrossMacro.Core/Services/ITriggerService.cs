
namespace CrossMacro.Core.Services;

/// <summary>
/// Polls the active window and runs configured trigger actions on match.
/// </summary>
public interface ITriggerService : IDisposable
{
    public ObservableCollection<TriggerTask> Tasks { get; }

    public bool IsMonitoring { get; }

    /// <summary>Completes when the current monitoring loop has stopped.</summary>
    public Task Completion { get; }

    public void AddTask(TriggerTask task);
    public void RemoveTask(Guid id);
    public void UpdateTask(TriggerTask task);
    public void SetTaskEnabled(Guid id, bool enabled);

    public void Start();
    public void StopMonitoring();

    /// <summary>Requests shutdown and exposes completion of the current monitoring lifetime.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default);

    public Task LoadAsync();
    public Task SaveAsync();
    public Task ReloadAsync(string profileConfigDirectory) => LoadAsync();

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired;
}
