
namespace CrossMacro.Core.Services.Automation.Shortcuts;

/// <summary>
/// Application-facing operations for shortcut tasks.
/// </summary>
/// <remarks>
/// This port contains manual execution only. Task snapshots and persistence are supplied by
/// <see cref="IShortcutTaskStore"/>. Shortcut listening lifecycle, events and hotkey integration
/// remain behind <see cref="IShortcutService"/>.
/// </remarks>
public interface IShortcutTaskOperations
{
    /// <summary>Synchronously resolves and binds the selected task before returning its execution task.</summary>
    /// <remarks>Callers may release the active-profile admission gate once this method returns; execution completion may be asynchronous.</remarks>
    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
