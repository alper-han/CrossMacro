namespace CrossMacro.Application.Automation;

/// <summary>The operations of task kinds that can also be run on demand (schedules and shortcuts).</summary>
public interface IManageRunnableAutomationTasks<TTask> : IManageAutomationTasks<TTask>
    where TTask : class
{
    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
