
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
