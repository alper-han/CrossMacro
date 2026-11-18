using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tools;

/// <summary>
/// The capability checks, path authorization and result mapping shared by the typed tools of one task kind
/// (schedules, shortcuts or triggers). A kind supplies its admission rules and its result contract.
/// </summary>
internal abstract class McpTaskToolKind<TTask, TCommand, TResult>(
    McpToolAuthorization authorization,
    ITaskCommands<TTask, TCommand> commands,
    AutomationTaskKind<TTask> kind)
    where TTask : class
    where TCommand : class, ITaskCommand
{
    protected McpToolAuthorization Authorization { get; } = authorization;

    protected AutomationTaskKind<TTask> Kind { get; } = kind;

    public async Task<TResult> ListAsync(CancellationToken cancellationToken)
    {
        var capability = Authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateResult("list", await commands.ListAsync(cancellationToken).ConfigureAwait(false), RedactMacroPaths())
            : CreateResult("list", capability);
    }

    public async Task<TResult> ExecuteAsync(string action, TCommand options, CancellationToken cancellationToken)
    {
        var capability = Authorization.RequireTaskManagement(RequiresInputAutomation(options), requiresMacroRead: RequiresMacroRead(options));
        if (capability is not null)
        {
            return CreateResult(action, capability);
        }

        var pathError = AuthorizePaths(options, out var authorizedOptions);
        if (pathError is not null)
        {
            return CreateResult(action, pathError);
        }

        if (EnablesStoredTask(authorizedOptions))
        {
            var taskAuthorization = await Authorization.TryAuthorizeTaskMacroAsync(authorizedOptions.TaskId ?? string.Empty, commands, Kind, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateResult(action, taskAuthorization);
            }
        }

        return await Authorization.RunWithTaskAuthorizationAsync(
            async () => CreateResult(action, await commands.ExecuteAsync(authorizedOptions, cancellationToken).ConfigureAwait(false), RedactMacroPaths()),
            outcome => CreateResult(action, outcome)).ConfigureAwait(false);
    }

    protected abstract bool RequiresInputAutomation(TCommand options);

    protected virtual bool RequiresMacroRead(TCommand options) => RequiresInputAutomation(options);

    /// <summary>Whether the command may enable the stored task, whose macro files must then be authorized.</summary>
    protected abstract bool EnablesStoredTask(TCommand options);

    /// <summary>Authorizes the macro paths the command carries; returns the denial, or null with the command to run.</summary>
    protected abstract McpToolOutcome? AuthorizePaths(TCommand options, out TCommand authorized);

    protected abstract TResult CreateResult(string action, McpToolOutcome outcome);

    protected abstract TResult CreateResult(string action, TaskCommandResult<TTask> result, bool redactMacroPaths = false);

    protected static McpToolOutcome CreateOutcome(bool success, string message) =>
        success ? McpToolOutcomeMapper.Success(message) : McpToolOutcomeMapper.InvalidArguments(message);

    private bool RedactMacroPaths() => !Authorization.IsAnyAllowed(McpCapability.MacroRead);
}
