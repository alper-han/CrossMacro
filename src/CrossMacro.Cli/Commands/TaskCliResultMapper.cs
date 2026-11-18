using CrossMacro.Core.Models.Automation.Scheduling;

namespace CrossMacro.Cli.Commands;

/// <summary>Converts the application results of one task kind to the stable CLI output shapes.</summary>
internal sealed class TaskCliResultMapper<TTask>(
    Func<IReadOnlyList<TTask>, object> mapList,
    Func<TTask, object> mapTask,
    Func<TTask, object>? mapRun = null)
    where TTask : class
{
    public OperationResult FromApplication(TaskCommandResult<TTask> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, result.Message, result.Errors);
        }

        object? data = null;
        if (result.Tasks is not null)
        {
            data = mapList(result.Tasks);
        }
        else if (result.Task is { } task)
        {
            data = result.WasRun && mapRun is not null ? mapRun(task) : mapTask(task);
        }

        return OperationResult.Ok(result.Message, data);
    }
}

/// <summary>The CLI output mappers of the schedule, shortcut and trigger task kinds.</summary>
internal static class TaskCliResultMapper
{
    public static TaskCliResultMapper<ScheduledTask> Schedule { get; } = new(
        static tasks => ToList(tasks, MapScheduleTask),
        MapScheduleTask,
        static task => new ScheduleTaskRunData(task.Id, task.Name, task.IsEnabled, task.MacroFilePath, task.LastRunTime, task.LastStatus));

    public static TaskCliResultMapper<ShortcutTask> Shortcut { get; } = new(
        static tasks => ToList(tasks, MapShortcutTask),
        MapShortcutTask,
        static task => new ShortcutTaskRunData(task.Id, task.Name, task.IsEnabled, task.Hotkeys.ToArray(), task.MacroFilePath, task.LastTriggeredTime, task.LastStatus, task.RunOnPressAndRelease, task.ReleaseMacroFilePath));

    public static TaskCliResultMapper<TriggerTask> Trigger { get; } = new(
        static tasks => ToList(tasks, MapTriggerTask),
        MapTriggerTask);

    private static TaskListData<TData> ToList<TTask, TData>(IReadOnlyList<TTask> tasks, Func<TTask, TData> map) =>
        new(tasks.Count, tasks.Select(map).ToArray());

    private static ScheduleTaskData MapScheduleTask(ScheduledTask task)
    {
        if (task.Type is ScheduleType.Weekly)
        {
            return new ScheduleTaskData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.Type.ToString(),
                task.MacroFilePath,
                task.PlaybackSpeed,
                IntervalValue: null,
                IntervalUnit: null,
                ScheduledDateTime: null,
                task.WeeklyDays.ToString(),
                task.WeeklyTime.ToString(),
                task.NextRunTime,
                task.LastRunTime,
                task.LastStatus);
        }

        return new ScheduleTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Type.ToString(),
            task.MacroFilePath,
            task.PlaybackSpeed,
            task.Type is ScheduleType.Interval ? task.IntervalValue : null,
            task.Type is ScheduleType.Interval ? task.IntervalUnit.ToString() : null,
            task.Type is ScheduleType.SpecificTime ? task.ScheduledDateTime : null,
            WeeklyDays: null,
            WeeklyTime: null,
            task.NextRunTime,
            task.LastRunTime,
            task.LastStatus);
    }

    private static ShortcutTaskData MapShortcutTask(ShortcutTask task)
    {
        return new ShortcutTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Hotkeys.ToArray(),
            task.MacroFilePath,
            task.PlaybackSpeed,
            task.LoopEnabled,
            task.RunWhileHeld,
            task.RepeatCount,
            task.RepeatDelayMs,
            task.UseRandomRepeatDelay,
            task.UseRandomRepeatDelay ? task.RepeatDelayMinMs : null,
            task.UseRandomRepeatDelay ? task.RepeatDelayMaxMs : null,
            task.WindowRules.ToArray(),
            task.LastTriggeredTime,
            task.LastStatus,
            task.RunOnPressAndRelease,
            task.ReleaseMacroFilePath);
    }

    private static TriggerTaskData MapTriggerTask(TriggerTask task)
    {
        return new TriggerTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Field.ToString(),
            task.MatchMode.ToString(),
            task.Value,
            task.Action.ToString(),
            task.Action is TriggerOperation.SwitchProfile ? task.TargetProfileId : null,
            task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null,
            task.FireMode.ToString(),
            task.CooldownMs,
            task.DebounceMs,
            task.LastTriggeredTime,
            task.LastStatus);
    }
}
