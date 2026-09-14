using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tests;

internal static class TaskCommandTestResults
{
    public static TaskCommandResult<ScheduledTask> Schedule(CliCommandExecutionResult result) => new(
        result.Success, result.Message, result.Errors,
        Tasks: (result.Data as TaskListData<ScheduleTaskData>)?.Tasks.Select(ToTask).ToArray(),
        Task: result.Data switch { ScheduleTaskData task => ToTask(task), ScheduleTaskRunData run => new ScheduledTask { Id = run.Id, Name = run.Name, IsEnabled = run.Enabled, MacroFilePath = run.MacroFilePath, LastRunTime = run.LastRunTime, LastStatus = run.LastStatus }, _ => null },
        WasRun: result.Data is ScheduleTaskRunData);

    public static TaskCommandResult<ShortcutTask> Shortcut(CliCommandExecutionResult result) => new(
        result.Success, result.Message, result.Errors,
        Tasks: (result.Data as TaskListData<ShortcutTaskData>)?.Tasks.Select(ToTask).ToArray(),
        Task: result.Data switch { ShortcutTaskData task => ToTask(task), ShortcutTaskRunData run => new ShortcutTask { Id = run.Id, Name = run.Name, IsEnabled = run.Enabled, HotkeyString = run.Hotkey, MacroFilePath = run.MacroFilePath, LastTriggeredTime = run.LastTriggeredTime, LastStatus = run.LastStatus }, _ => null },
        WasRun: result.Data is ShortcutTaskRunData);

    public static TaskCommandResult<TriggerTask> Trigger(CliCommandExecutionResult result) => new(
        result.Success, result.Message, result.Errors,
        Tasks: (result.Data as TaskListData<TriggerTaskData>)?.Tasks.Select(ToTask).ToArray(),
        Task: result.Data is TriggerTaskData task ? ToTask(task) : null);

    private static ScheduledTask ToTask(ScheduleTaskData task) => new()
    {
        Id = task.Id, Name = task.Name, IsEnabled = task.Enabled, Type = Enum.Parse<ScheduleType>(task.Type),
        MacroFilePath = task.MacroFilePath, PlaybackSpeed = task.PlaybackSpeed,
        IntervalValue = task.IntervalValue ?? 1, IntervalUnit = task.IntervalUnit is {} unit ? Enum.Parse<IntervalUnit>(unit) : IntervalUnit.Minutes,
        ScheduledDateTime = task.ScheduledDateTime ?? DateTime.Now,
        WeeklyDays = task.WeeklyDays is {} days ? Enum.Parse<ScheduleDays>(days) : ScheduleDays.None,
        WeeklyTime = task.WeeklyTime is {} time ? TimeSpan.Parse(time, System.Globalization.CultureInfo.InvariantCulture) : TimeSpan.Zero,
        NextRunTime = task.NextRunTime, LastRunTime = task.LastRunTime, LastStatus = task.LastStatus,
    };

    private static ShortcutTask ToTask(ShortcutTaskData task)
    {
        var result = new ShortcutTask
        {
            Id = task.Id, Name = task.Name, IsEnabled = task.Enabled, HotkeyString = task.Hotkey, MacroFilePath = task.MacroFilePath,
            PlaybackSpeed = task.PlaybackSpeed, LoopEnabled = task.LoopEnabled, RunWhileHeld = task.RunWhileHeld,
            RepeatCount = task.RepeatCount, RepeatDelayMs = task.RepeatDelayMs, UseRandomRepeatDelay = task.RandomRepeatDelay,
            RepeatDelayMinMs = task.RepeatDelayMinMs ?? 0, RepeatDelayMaxMs = task.RepeatDelayMaxMs ?? 0,
            LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
        };
        foreach (var rule in task.WindowRules)
        {
            result.WindowRules.Add(rule);
        }
        return result;
    }

    private static TriggerTask ToTask(TriggerTaskData task) => new()
    {
        Id = task.Id, Name = task.Name, IsEnabled = task.Enabled, Field = Enum.Parse<TriggerField>(task.Field),
        MatchMode = Enum.Parse<TriggerMatchMode>(task.MatchMode), Value = task.Value, Action = Enum.Parse<TriggerOperation>(task.Action),
        TargetProfileId = task.TargetProfileId ?? string.Empty, MacroFilePath = task.MacroFilePath ?? string.Empty,
        FireMode = Enum.Parse<TriggerFireMode>(task.FireMode), CooldownMs = task.CooldownMs, DebounceMs = task.DebounceMs,
        LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };
}
