using CrossMacro.Core.Services.Automation;

namespace CrossMacro.Application.Automation;

/// <summary>
/// A task management workflow for kinds whose tasks can also be run on demand. <paramref name="run"/> starts
/// the bound task; it is the kind's manual-execution port (for example <see cref="IScheduledTaskOperations.RunTaskAsync"/>).
/// </summary>
public sealed class ManageRunnableAutomationTasks<TTask>(
    AutomationTaskKind<TTask> kind,
    IAutomationTaskStore<TTask> store,
    Func<Guid, CancellationToken, Task> run,
    AutomationTaskMutationGate? mutationGate = null)
    : ManageAutomationTasks<TTask>(kind, store, mutationGate), IManageRunnableAutomationTasks<TTask>
    where TTask : class
{
    private readonly Func<Guid, CancellationToken, Task> _run = run ?? throw new ArgumentNullException(nameof(run));

    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default) =>
        RunBoundAsync(request, _run, cancellationToken);
}
