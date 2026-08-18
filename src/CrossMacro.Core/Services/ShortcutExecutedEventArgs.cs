
namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for shortcut execution events
/// </summary>
public class ShortcutExecutedEventArgs(
    ShortcutTask task,
    bool success,
    string? message = null) : EventArgs
{
    public ShortcutTask Task { get; } = task;
    public bool Success { get; } = success;
    public string? Message { get; } = message;
}
