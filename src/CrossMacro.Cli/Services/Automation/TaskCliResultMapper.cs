namespace CrossMacro.Cli.Services.Automation;

/// <summary>Converts application task results to the stable CLI output shapes.</summary>
internal static class TaskCliResultMapper
{
    public static CliCommandExecutionResult FromApplication(TaskCommandResult<ScheduledTask> result)
    {
        if (!result.Success)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.Message, result.Errors);
        }
        object? data = null;
        if (result.Tasks is not null)
        {
            data = new TaskListData<ScheduleTaskData>(result.Tasks.Count, result.Tasks.Select(MapScheduleTask).ToArray());
        }
        else if (result.Task is { } task)
        {
            data = result.WasRun ? new ScheduleTaskRunData(task.Id, task.Name, task.IsEnabled, task.MacroFilePath, task.LastRunTime, task.LastStatus) : MapScheduleTask(task);
        }
        return CliCommandExecutionResult.Ok(result.Message, data);
    }

    public static CliCommandExecutionResult FromApplication(TaskCommandResult<ShortcutTask> result)
    {
        if (!result.Success)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.Message, result.Errors);
        }
        object? data = null;
        if (result.Tasks is not null)
        {
            data = new TaskListData<ShortcutTaskData>(result.Tasks.Count, result.Tasks.Select(MapTask).ToArray());
        }
        else if (result.Task is { } task)
        {
            data = result.WasRun ? new ShortcutTaskRunData(task.Id, task.Name, task.IsEnabled, task.HotkeyString, task.MacroFilePath, task.LastTriggeredTime, task.LastStatus) : MapTask(task);
        }
        return CliCommandExecutionResult.Ok(result.Message, data);
    }

    public static CliCommandExecutionResult FromApplication(TaskCommandResult<TriggerTask> result)
    {
        if (!result.Success)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.Message, result.Errors);
        }
        object? data = null;
        if (result.Tasks is not null)
        {
            data = new TaskListData<TriggerTaskData>(result.Tasks.Count, result.Tasks.Select(MapTask).ToArray());
        }
        else if (result.Task is { } task)
        {
            data = MapTask(task);
        }
        return CliCommandExecutionResult.Ok(result.Message, data);
    }

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
                task.LastStatus
            );
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
            task.LastStatus
        );
    }

    private static ShortcutTaskData MapTask(ShortcutTask task)
    {
        return new ShortcutTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.HotkeyString,
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
            task.LastStatus);
    }

    private static TriggerTaskData MapTask(TriggerTask task)
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
