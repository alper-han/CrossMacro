using CrossMacro.Application.Automation;
using CrossMacro.Core.Models.Automation.Triggers;

namespace CrossMacro.Mcp.Tools;

internal sealed class McpTriggerToolKind(McpToolAuthorization authorization, ITaskCommands<TriggerTask, TriggerCommand> commands)
    : McpTaskToolKind<TriggerTask, TriggerCommand, McpTriggerResult>(authorization, commands, TriggerTaskKind.Instance)
{
    protected override bool RequiresInputAutomation(TriggerCommand options) => McpTaskAdmission.RequiresInputAutomation(options);

    protected override bool RequiresMacroRead(TriggerCommand options) => McpTaskAdmission.RequiresMacroRead(options);

    protected override bool EnablesStoredTask(TriggerCommand options) => McpTaskAdmission.EnablesStoredTask(options);

    protected override McpToolOutcome? AuthorizePaths(TriggerCommand options, out TriggerCommand authorized)
    {
        authorized = options;
        if (!Authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var macroPath, out var error))
        {
            return error;
        }

        authorized = options with { MacroFilePath = macroPath };
        return null;
    }

    protected override McpTriggerResult CreateResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Task: null);

    protected override McpTriggerResult CreateResult(string action, TaskCommandResult<TriggerTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToTask(task, redactMacroPaths)).ToArray() ?? [],
        Task: result.Task is { } task ? ToTask(task, redactMacroPaths) : null);

    private static McpTriggerTask ToTask(TriggerTask task, bool redactMacroPath) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        Field = task.Field.ToString(),
        MatchMode = task.MatchMode.ToString(),
        Value = task.Value,
        Action = task.Action.ToString(),
        TargetProfileId = task.Action is TriggerOperation.SwitchProfile ? task.TargetProfileId : null,
        MacroFilePath = !redactMacroPath && task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null,
        FireMode = task.FireMode.ToString(),
        CooldownMs = task.CooldownMs,
        DebounceMs = task.DebounceMs,
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
    };
}
