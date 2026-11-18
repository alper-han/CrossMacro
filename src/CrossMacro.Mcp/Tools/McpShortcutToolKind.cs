using CrossMacro.Application.Automation;
using CrossMacro.Core.Models.Automation.Shortcuts;

namespace CrossMacro.Mcp.Tools;

internal sealed class McpShortcutToolKind(McpToolAuthorization authorization, IRunnableTaskCommands<ShortcutTask, ShortcutCommand> commands)
    : McpRunnableTaskToolKind<ShortcutTask, ShortcutCommand, McpShortcutResult>(authorization, commands, ShortcutTaskKind.Instance)
{
    protected override bool RequiresInputAutomation(ShortcutCommand options) => McpTaskAdmission.RequiresInputAutomation(options);

    protected override bool EnablesStoredTask(ShortcutCommand options) => McpTaskAdmission.EnablesStoredTask(options);

    protected override McpToolOutcome? AuthorizePaths(ShortcutCommand options, out ShortcutCommand authorized)
    {
        authorized = options;
        if (!Authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var macroPath, out var error))
        {
            return error;
        }

        if (!Authorization.TryAuthorizeTaskMacroPath(options.ReleaseMacroFilePath, out var releasePath, out error))
        {
            return error;
        }

        authorized = options with { MacroFilePath = macroPath, ReleaseMacroFilePath = releasePath };
        return null;
    }

    protected override McpShortcutResult CreateResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    protected override McpShortcutResult CreateResult(string action, TaskCommandResult<ShortcutTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToTask(task, redactMacroPaths)).ToArray() ?? [],
        Run: result.WasRun && result.Task is { } run ? ToTaskRun(run, redactMacroPaths) : null,
        Task: !result.WasRun && result.Task is { } task ? ToTask(task, redactMacroPaths) : null);

    private static McpShortcutTask ToTask(ShortcutTask task, bool redactMacroPath) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        Hotkeys = task.Hotkeys.ToArray(),
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath,
        PlaybackSpeed = task.PlaybackSpeed,
        RunOnPressAndRelease = task.RunOnPressAndRelease,
        ReleaseMacroFilePath = redactMacroPath ? string.Empty : task.ReleaseMacroFilePath,
        LoopEnabled = task.LoopEnabled,
        RunWhileHeld = task.RunWhileHeld,
        RepeatCount = task.RepeatCount,
        RepeatDelayMs = task.RepeatDelayMs,
        RandomRepeatDelay = task.UseRandomRepeatDelay,
        RepeatDelayMinMs = task.UseRandomRepeatDelay ? task.RepeatDelayMinMs : null,
        RepeatDelayMaxMs = task.UseRandomRepeatDelay ? task.RepeatDelayMaxMs : null,
        WindowRules = task.WindowRules.Select(static rule => new McpShortcutWindowRule { Field = rule.Field.ToString(), MatchMode = rule.MatchMode.ToString(), Value = rule.Value }).ToArray(),
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
    };

    private static McpShortcutTaskRun ToTaskRun(ShortcutTask task, bool redactMacroPath) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        Hotkeys = task.Hotkeys.ToArray(),
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath,
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
        RunOnPressAndRelease = task.RunOnPressAndRelease,
        ReleaseMacroFilePath = redactMacroPath ? string.Empty : task.ReleaseMacroFilePath,
    };
}
