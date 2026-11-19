using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tools;

/// <summary>The typed tools of a task kind that can also be run on demand.</summary>
internal abstract class McpRunnableTaskToolKind<TTask, TCommand, TResult>(
    McpToolAuthorization authorization,
    IRunnableTaskCommands<TTask, TCommand> commands,
    AutomationTaskKind<TTask> kind)
    : McpTaskToolKind<TTask, TCommand, TResult>(authorization, commands, kind)
    where TTask : class
    where TCommand : class, ITaskCommand
{
    private readonly IRunnableTaskCommands<TTask, TCommand> _runnableCommands = commands;

    public async Task<TResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        var capability = Authorization.RequireTaskManagement(requiresInputAutomation: true, requiresMacroRead: true);
        if (capability is not null)
        {
            return CreateResult("run", capability);
        }

        var taskAuthorization = await Authorization.TryAuthorizeTaskMacroAsync(taskId, _runnableCommands, Kind, cancellationToken).ConfigureAwait(false);
        return taskAuthorization is null
            ? await Authorization.RunWithTaskAuthorizationAsync(
                async () => CreateResult("run", await _runnableCommands.RunAsync(taskId, cancellationToken).ConfigureAwait(false)),
                outcome => CreateResult("run", outcome)).ConfigureAwait(false)
            : CreateResult("run", taskAuthorization);
    }
}
