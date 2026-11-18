using CrossMacro.Application.Automation;
using CrossMacro.Core.Models.Automation.Scheduling;

namespace CrossMacro.Mcp.Tools;

internal sealed class McpScheduleToolKind(McpToolAuthorization authorization, IRunnableTaskCommands<ScheduledTask, ScheduleCommand> commands)
    : McpRunnableTaskToolKind<ScheduledTask, ScheduleCommand, McpScheduleResult>(authorization, commands, ScheduleTaskKind.Instance)
{
    protected override bool RequiresInputAutomation(ScheduleCommand options) => McpTaskAdmission.RequiresInputAutomation(options);

    protected override bool EnablesStoredTask(ScheduleCommand options) => McpTaskAdmission.EnablesStoredTask(options);

    protected override McpToolOutcome? AuthorizePaths(ScheduleCommand options, out ScheduleCommand authorized)
    {
        authorized = options;
        if (!Authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var macroPath, out var error))
        {
            return error;
        }

        authorized = options with { MacroFilePath = macroPath };
        return null;
    }

    protected override McpScheduleResult CreateResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    protected override McpScheduleResult CreateResult(string action, TaskCommandResult<ScheduledTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToTask(task, redactMacroPaths)).ToArray() ?? [],
        Run: result.WasRun && result.Task is { } run ? ToTaskRun(run, redactMacroPaths) : null,
        Task: !result.WasRun && result.Task is { } task ? ToTask(task, redactMacroPaths) : null);

    private static McpScheduleTask ToTask(ScheduledTask task, bool redactMacroPath) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        Type = task.Type.ToString(),
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath,
        PlaybackSpeed = task.PlaybackSpeed,
        IntervalValue = task.Type is ScheduleType.Interval ? task.IntervalValue : null,
        IntervalUnit = task.Type is ScheduleType.Interval ? task.IntervalUnit.ToString() : null,
        ScheduledDateTime = task.Type is ScheduleType.SpecificTime ? task.ScheduledDateTime : null,
        WeeklyDays = task.Type is ScheduleType.Weekly ? task.WeeklyDays.ToString() : null,
        WeeklyTime = task.Type is ScheduleType.Weekly ? task.WeeklyTime.ToString() : null,
        NextRunTime = task.NextRunTime,
        LastRunTime = task.LastRunTime,
        LastStatus = task.LastStatus,
    };

    private static McpScheduleTaskRun ToTaskRun(ScheduledTask task, bool redactMacroPath) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath,
        LastRunTime = task.LastRunTime,
        LastStatus = task.LastStatus,
    };
}
