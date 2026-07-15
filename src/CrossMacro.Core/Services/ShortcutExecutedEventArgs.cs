
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
