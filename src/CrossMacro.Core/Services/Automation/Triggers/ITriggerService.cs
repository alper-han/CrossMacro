
namespace CrossMacro.Core.Services.Automation.Triggers;

/// <summary>
/// Polls the active window and runs configured trigger actions on match.
/// </summary>
public interface ITriggerService : IDisposable
{
    public ObservableCollection<TriggerTask> Tasks { get; }

    /// <summary>Checks runtime object identity under the service's task synchronization.</summary>
    public bool IsCurrentTask(TriggerTask task);

    public bool IsMonitoring { get; }

    /// <summary>Completes when the current monitoring loop has stopped.</summary>
    public Task Completion { get; }

    public void Start();
    public void StopMonitoring();

    /// <summary>Requests shutdown and exposes completion of the current monitoring lifetime.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default);

    public Task LoadAsync();
    public Task SaveAsync();
    public Task ReloadAsync(string profileConfigDirectory);

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired;
}
