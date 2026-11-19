namespace CrossMacro.Application.Automation;

/// <summary>The command set of task kinds that can also be run on demand.</summary>
public sealed class RunnableTaskCommands<TTask, TCommand>(
    IManageRunnableAutomationTasks<TTask> manage,
    AutomationTaskKind<TTask> kind,
    TaskCommandRules<TTask, TCommand> rules)
    : TaskCommands<TTask, TCommand>(manage, kind, rules), IRunnableTaskCommands<TTask, TCommand>
    where TTask : class, new()
    where TCommand : class, ITaskCommand
{
    private readonly IManageRunnableAutomationTasks<TTask> _manage = manage;

    public Task<TaskCommandResult<TTask>> RunAsync(string taskId, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var (task, failure, scopeGeneration) = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            await _manage.RunAsync(new TaskRequest(Kind.IdOf(task!), ExpectedScopeGeneration: scopeGeneration), cancellationToken).ConfigureAwait(false);
            return new TaskCommandResult<TTask>(Success: true, $"{Kind.Title} task executed.", [], Task: task, WasRun: true);
        });
}
