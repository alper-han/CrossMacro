namespace CrossMacro.Core.Services.Automation.Scheduling;

public sealed class ScheduledTaskStartingEventArgs(ScheduledTask task) : EventArgs
{
    public ScheduledTask Task { get; } = task ?? throw new ArgumentNullException(nameof(task));
}
