namespace CrossMacro.Core.Services;

public sealed class ScheduledTaskStartingEventArgs : EventArgs
{
    public ScheduledTaskStartingEventArgs(ScheduledTask task)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
    }

    public ScheduledTask Task { get; }
}
