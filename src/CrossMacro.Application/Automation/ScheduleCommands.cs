using System.Globalization;

namespace CrossMacro.Application.Automation;

public sealed class ScheduleCommands(IManageSchedule manageSchedule) : TaskCommands<ScheduledTask>(manageSchedule), IScheduleCommands
{
    private readonly IManageSchedule _manageSchedule = manageSchedule;

    protected override string Kind => "schedule";

    protected override string CannotEnableReason => "Schedule task requires a macro path and valid schedule fields before it can be enabled.";

    protected override Guid IdOf(ScheduledTask task) => task.Id;

    protected override string NameOf(ScheduledTask task) => task.Name;

    protected override bool CanBeEnabled(ScheduledTask task) => task.CanBeEnabled;

    public Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken) =>
        GuardAsync(() => RunCoreAsync(taskId, cancellationToken));

    public Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken) =>
        GuardAsync(() => ExecuteCoreAsync(options, cancellationToken));

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

        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
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

        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageSchedule.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ScheduledTask>($"Schedule task updated: {task.Name}.", task);
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
                return TaskCommandResult.Fail<ScheduledTask>("Invalid schedule datetime.", [$"Could not parse the date and time value: {options.At}"]);
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

            TimeSpan? weeklyTime = null;
            if (!string.IsNullOrWhiteSpace(options.Time))
            {
                if (!TimeSpan.TryParse(options.Time, CultureInfo.InvariantCulture, out var parsedTime))
                {
                    return TaskCommandResult.Fail<ScheduledTask>("Invalid weekly schedule time.", [$"Could not parse the time value: {options.Time}"]);
                }

                weeklyTime = parsedTime;
            }

            task.Type = ScheduleType.Weekly;
            task.WeeklyDays = days;
            if (weeklyTime is { } time)
            {
                task.WeeklyTime = time;
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

        if (!char.IsLetter(suffix))
        {
            return true;
        }

        switch (char.ToLowerInvariant(suffix))
        {
            case 's':
                return true;
            case 'm':
                intervalUnit = IntervalUnit.Minutes;
                return true;
            case 'h':
                intervalUnit = IntervalUnit.Hours;
                return true;
            default:
                error = $"Unsupported interval suffix in {value}. Use s, m, or h.";
                return false;
        }
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
