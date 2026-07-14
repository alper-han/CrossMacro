using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for trigger fires.
/// </summary>
public class TriggerFiredEventArgs : EventArgs
{
    public TriggerTask Task { get; }
    public bool Success { get; }
    public string? Message { get; }

    public TriggerFiredEventArgs(TriggerTask task, bool success, string? message = null)
    {
        Task = task;
        Success = success;
        Message = message;
    }
}

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
