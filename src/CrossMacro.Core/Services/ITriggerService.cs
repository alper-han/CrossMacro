
namespace CrossMacro.Core.Services;

/// <summary>
/// Polls the active window and runs configured trigger actions on match.
/// </summary>
public interface ITriggerService : IDisposable
{
    ObservableCollection<TriggerTask> Tasks { get; }

    bool IsMonitoring { get; }

    /// <summary>Completes when the current monitoring loop has stopped.</summary>
    Task Completion { get; }

    void AddTask(TriggerTask task);
    void RemoveTask(Guid id);
    void UpdateTask(TriggerTask task);
    void SetTaskEnabled(Guid id, bool enabled);

    void Start();
    void Stop();

    /// <summary>Requests shutdown and exposes completion of the current monitoring lifetime.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    Task LoadAsync();
    Task SaveAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();

    event EventHandler<TriggerFiredEventArgs>? TriggerFired;
}
