using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for shortcut execution events
/// </summary>
public class ShortcutExecutedEventArgs : EventArgs
{
    public ShortcutTask Task { get; }
    public bool Success { get; }
    public string? Message { get; }
    
    public ShortcutExecutedEventArgs(ShortcutTask task, bool success, string? message = null)
    {
        Task = task;
        Success = success;
        Message = message;
    }
}

/// <summary>
/// Interface for macro shortcut service
/// </summary>
public interface IShortcutService : IDisposable
{
    /// <summary>
    /// Collection of shortcut tasks
    /// </summary>
    ObservableCollection<ShortcutTask> Tasks { get; }
    
    /// <summary>
    /// Whether the service is listening for shortcuts
    /// </summary>
    bool IsListening { get; }
    
    /// <summary>
    /// Adds a new shortcut task
    /// </summary>
    void AddTask(ShortcutTask task);
    
    /// <summary>
    /// Removes a shortcut task by ID
    /// </summary>
    void RemoveTask(Guid id);
    
    /// <summary>
    /// Updates an existing task
    /// </summary>
    void UpdateTask(ShortcutTask task);
    
    /// <summary>
    /// Enables or disables a task
    /// </summary>
    void SetTaskEnabled(Guid id, bool enabled);
    
    /// <summary>
    /// Starts listening for shortcuts
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stops listening for shortcuts
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Saves tasks to persistent storage
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Loads tasks from persistent storage
    /// </summary>
    Task LoadAsync();
    
    /// <summary>
    /// Event fired when a shortcut is executed
    /// </summary>
    event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
    
    /// <summary>
    /// Event fired when a shortcut starts executing
    /// </summary>
    event EventHandler<ShortcutTask>? ShortcutStarting;
}
