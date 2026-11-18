using System.Globalization;

namespace CrossMacro.Application.Automation;

public sealed class ScheduleTaskCommandRules : TaskCommandRules<ScheduledTask, ScheduleCommand>
{
    public static ScheduleTaskCommandRules Instance { get; } = new();

    private ScheduleTaskCommandRules()
    {
    }

    public override Func<ScheduledTask, string>? DescribeNext { get; } = static task =>
    {
        if (task.NextRunTime is null && task.CanBeEnabled)
        {
            task.CalculateNextRunTime();
        }

        return $"Next run for schedule task: {task.NextRunTime?.ToString("O", CultureInfo.InvariantCulture) ?? "none"}.";
    };

    public override TaskCommandResult<ScheduledTask>? ApplyAdd(ScheduledTask task, ScheduleCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        task.Name = command.Name ?? string.Empty;
        task.MacroFilePath = command.MacroFilePath ?? string.Empty;
        return ApplyScheduleAndSpeed(task, command);
    }

    public override TaskCommandResult<ScheduledTask>? ApplyEdit(ScheduledTask task, ScheduleCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            task.Name = command.Name;
        }

        if (!string.IsNullOrWhiteSpace(command.MacroFilePath))
        {
            task.MacroFilePath = command.MacroFilePath;
        }

        return ApplyScheduleAndSpeed(task, command);
    }

    private static TaskCommandResult<ScheduledTask>? ApplyScheduleAndSpeed(ScheduledTask task, ScheduleCommand command)
    {
        var scheduleResult = ApplyScheduleOptions(task, command);
        if (scheduleResult is not null)
        {
            return scheduleResult;
        }

        if (command.Speed is not null)
        {
            task.PlaybackSpeed = command.Speed.Value;
        }

        return null;
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
