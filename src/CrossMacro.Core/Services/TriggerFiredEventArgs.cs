
namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for trigger fires.
/// </summary>
public class TriggerFiredEventArgs(
    TriggerTask task,
    bool success,
    string? message = null) : EventArgs
{
    public TriggerTask Task { get; } = task;
    public bool Success { get; } = success;
    public string? Message { get; } = message;
}
