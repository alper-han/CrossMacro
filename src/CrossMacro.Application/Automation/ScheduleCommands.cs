using System.Globalization;

namespace CrossMacro.Application.Automation;

public sealed class ScheduleCommands(IManageSchedule manageSchedule) : IScheduleCommands
{
    private readonly IManageSchedule _manageSchedule = manageSchedule;

    public async Task<TaskCommandResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false);
            return new(Success: true, $"Loaded {result.Tasks.Count} schedule task(s).", [], Tasks: result.Tasks);
        }
        catch (TaskScopeUnavailableException ex)
        {
            return TaskCommandResult.Fail<ScheduledTask>(ex.Message);
        }
    }

    public async Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            return await RunCoreAsync(taskId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<ScheduledTask>(ex.Message);
        }
    }

    private async Task<TaskCommandResult<ScheduledTask>> RunCoreAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }
        await _manageSchedule.RunAsync(new TaskRequest(parsed.Task!.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return new(Success: true, "Schedule task executed.", [], Task: parsed.Task, WasRun: true);
    }

    public async Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<ScheduledTask>(ex.Message);
        }
    }

    private async Task<TaskCommandResult<ScheduledTask>> ExecuteCoreAsync(ScheduleCommand options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            ScheduleCommandAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            ScheduleCommandAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            ScheduleCommandAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            ScheduleCommandAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            ScheduleCommandAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            ScheduleCommandAction.Next => await NextAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            _ => TaskCommandResult.Fail<ScheduledTask>("Unknown schedule action."),
        };
    }

    private async Task<TaskCommandResult<ScheduledTask>> AddAsync(ScheduleCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false);
        var task = new ScheduledTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
        };

        var scheduleResult = ApplyScheduleOptions(task, options);
        if (scheduleResult is not null)
        {
            return scheduleResult;
        }

        if (options.Speed is not null)
        {
            task.PlaybackSpeed = options.Speed.Value;
        }

        if (options.Enabled is not null)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageSchedule.AddAsync(task, scope.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ScheduledTask>($"Schedule task added: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ScheduledTask>> EditAsync(ScheduleCommand options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = AutomationTaskSnapshots.Copy(parsed.Task!);
        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            task.Name = options.Name;
        }

        if (!string.IsNullOrWhiteSpace(options.MacroFilePath))
        {
            task.MacroFilePath = options.MacroFilePath;
        }

        var scheduleResult = ApplyScheduleOptions(task, options);
        if (scheduleResult is not null)
        {
            return scheduleResult;
        }

        if (options.Speed is not null)
        {
            task.PlaybackSpeed = options.Speed.Value;
        }

        if (options.Enabled is not null)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageSchedule.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ScheduledTask>($"Schedule task updated: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ScheduledTask>> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageSchedule.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ScheduledTask>($"Schedule task removed: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ScheduledTask>> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return TaskCommandResult.Fail<ScheduledTask>(
                "Schedule task cannot be enabled.",
                ["Schedule task requires a macro path and valid schedule fields before it can be enabled."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageSchedule.SetEnabledAsync(new TaskRequest(task.Id, enabled, parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return TaskCommandResult.Ok<ScheduledTask>($"Schedule task {verb}: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ScheduledTask>> NextAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (task.NextRunTime is null && task.CanBeEnabled)
        {
            task.CalculateNextRunTime();
        }

        return TaskCommandResult.Ok<ScheduledTask>($"Next run for schedule task: {task.NextRunTime?.ToString("O", CultureInfo.InvariantCulture) ?? "none"}.", task);
    }

    private async Task<(ScheduledTask? Task, TaskCommandResult<ScheduledTask>? Result, long ScopeGeneration)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id))
        {
            return (null, TaskCommandResult.Fail<ScheduledTask>("Invalid schedule task id format.", [$"Task id is not a valid GUID: {taskId}"]), 0);
        }
        var tasks = await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = tasks.Tasks.FirstOrDefault(candidate => candidate.Id == id);
        return task is null
            ? (null, TaskCommandResult.Fail<ScheduledTask>("Schedule task not found.", [$"No schedule task found with id: {taskId}"]), tasks.ScopeGeneration)
            : (task, null, tasks.ScopeGeneration);
    }

    private static TaskCommandResult<ScheduledTask>? ApplyScheduleOptions(ScheduledTask task, ScheduleCommand options)
    {
        if (!string.IsNullOrWhiteSpace(options.Interval))
        {
            if (!TryParseInterval(options.Interval, out var intervalValue, out var intervalUnit, out var error))
            {
                return TaskCommandResult.Fail<ScheduledTask>("Invalid schedule interval.", [error]);
            }

            task.Type = ScheduleType.Interval;
            task.IntervalValue = intervalValue;
            task.IntervalUnit = intervalUnit;
            task.ScheduledDateTime = null;
            return null;
        }

        if (!string.IsNullOrWhiteSpace(options.At))
        {
            if (!DateTime.TryParse(options.At, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var scheduledAt))
            {
                return TaskCommandResult.Fail<ScheduledTask>("Invalid schedule datetime.", [$"Could not parse --at value: {options.At}"]);
            }

            task.Type = ScheduleType.SpecificTime;
            task.ScheduledDateTime = scheduledAt;
            return null;
        }

        if (!string.IsNullOrWhiteSpace(options.Weekly))
        {
            if (!TryParseScheduleDays(options.Weekly, out var days, out var error))
            {
                return TaskCommandResult.Fail<ScheduledTask>("Invalid weekly schedule days.", [error]);
            }

            if (!string.IsNullOrWhiteSpace(options.Time)
                && !TimeSpan.TryParse(options.Time, CultureInfo.InvariantCulture, out _))
            {
                return TaskCommandResult.Fail<ScheduledTask>("Invalid weekly schedule time.", [$"Could not parse --time value: {options.Time}"]);
            }

            task.Type = ScheduleType.Weekly;
            task.WeeklyDays = days;
            if (!string.IsNullOrWhiteSpace(options.Time))
            {
                task.WeeklyTime = TimeSpan.Parse(options.Time, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static bool TryParseInterval(string value, out int intervalValue, out IntervalUnit intervalUnit, out string error)
    {
        intervalValue = 0;
        intervalUnit = IntervalUnit.Seconds;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Interval cannot be empty.";
            return false;
        }

        var suffix = value[^1];
        var numericPart = char.IsLetter(suffix) ? value[..^1] : value;
        if (!int.TryParse(numericPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalValue) || intervalValue < 1)
        {
            error = $"Interval must be a positive integer with optional s/m/h suffix: {value}";
            return false;
        }

        intervalUnit = char.ToLowerInvariant(suffix) switch
        {
            's' => IntervalUnit.Seconds,
            'm' => IntervalUnit.Minutes,
            'h' => IntervalUnit.Hours,
            _ when !char.IsLetter(suffix) => IntervalUnit.Seconds,
            _ => IntervalUnit.Seconds,
        };

        if (char.IsLetter(suffix) && suffix is not ('s' or 'S' or 'm' or 'M' or 'h' or 'H'))
        {
            error = $"Unsupported interval suffix in {value}. Use s, m, or h.";
            return false;
        }

        return true;
    }

    private static bool TryParseScheduleDays(string value, out ScheduleDays days, out string error)
    {
        days = ScheduleDays.None;
        error = string.Empty;
        foreach (var rawPart in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.ToUpperInvariant();
            var parsed = part switch
            {
                "MON" or "MONDAY" => ScheduleDays.Monday,
                "TUE" or "TUESDAY" => ScheduleDays.Tuesday,
                "WED" or "WEDNESDAY" => ScheduleDays.Wednesday,
                "THU" or "THURSDAY" => ScheduleDays.Thursday,
                "FRI" or "FRIDAY" => ScheduleDays.Friday,
                "SAT" or "SATURDAY" => ScheduleDays.Saturday,
                "SUN" or "SUNDAY" => ScheduleDays.Sunday,
                "WEEKDAYS" => ScheduleDays.Weekdays,
                "WEEKENDS" => ScheduleDays.Weekends,
                "EVERYDAY" or "DAILY" or "ALL" => ScheduleDays.EveryDay,
                _ => ScheduleDays.None,
            };

            if (parsed is ScheduleDays.None)
            {
                error = $"Unknown weekly day: {rawPart}";
                return false;
            }

            days |= parsed;
        }

        if (days is ScheduleDays.None)
        {
            error = "Weekly schedule requires at least one day.";
            return false;
        }

        return true;
    }



}
