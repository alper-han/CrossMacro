using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Services;

public sealed class ScheduleCliService : IScheduleCliService
{
    private readonly IManageSchedule _manageSchedule;
    private IReadOnlyList<ScheduledTask> _tasks = [];

    public ScheduleCliService(IManageSchedule manageSchedule)
    {
        _manageSchedule = manageSchedule;
    }

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "schedule",
            cancellationToken: cancellationToken,
            loadAsync: async () => _tasks = (await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
            mapTask: MapScheduleTask);
    }

    public async Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.RunTaskAsync(
            taskId: taskId,
            taskKindLower: "schedule",
            taskKindDisplay: "Schedule",
            cancellationToken: cancellationToken,
            loadAsync: async () => _tasks = (await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
            getTaskId: x => x.Id,
            runTaskAsync: (parsedTaskId, cancellationToken) => _manageSchedule.RunAsync(new TaskRequest(parsedTaskId), cancellationToken),
            mapTaskResult: task => new ScheduleTaskRunData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.MacroFilePath,
                task.LastRunTime,
                task.LastStatus
            ));
    }

    public Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ScheduleCliAction.Add => AddAsync(options, cancellationToken),
            ScheduleCliAction.Edit => EditAsync(options, cancellationToken),
            ScheduleCliAction.Remove => RemoveAsync(options.TaskId ?? string.Empty, cancellationToken),
            ScheduleCliAction.Enable => SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken),
            ScheduleCliAction.Disable => SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken),
            ScheduleCliAction.Next => NextAsync(options.TaskId ?? string.Empty, cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown schedule action.")),
        };
    }

    private async Task<CliCommandExecutionResult> AddAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = new ScheduledTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
        };

        var scheduleResult = ApplyScheduleOptions(task, options);
        if (scheduleResult is not null) return scheduleResult;

        if (options.Speed.HasValue)
        {
            task.PlaybackSpeed = options.Speed.Value;
        }

        if (options.Enabled.HasValue)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _manageSchedule.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Schedule task added: {task.Name}.", MapScheduleTask(task));
    }

    private async Task<CliCommandExecutionResult> EditAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (!string.IsNullOrWhiteSpace(options.Name)) task.Name = options.Name;
        if (!string.IsNullOrWhiteSpace(options.MacroFilePath)) task.MacroFilePath = options.MacroFilePath;

        var scheduleResult = ApplyScheduleOptions(task, options);
        if (scheduleResult is not null) return scheduleResult;

        if (options.Speed.HasValue) task.PlaybackSpeed = options.Speed.Value;
        if (options.Enabled.HasValue) task.IsEnabled = options.Enabled.Value;

        cancellationToken.ThrowIfCancellationRequested();
        await _manageSchedule.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Schedule task updated: {task.Name}.", MapScheduleTask(task));
    }

    private async Task<CliCommandExecutionResult> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        await _manageSchedule.RemoveAsync(new TaskRequest(task.Id), cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Schedule task removed: {task.Name}.", MapScheduleTask(task));
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Schedule task cannot be enabled.",
                ["Schedule task requires a macro path and valid schedule fields before it can be enabled."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _manageSchedule.SetEnabledAsync(new TaskRequest(task.Id, enabled), cancellationToken).ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return CliCommandExecutionResult.Ok($"Schedule task {verb}: {task.Name}.", MapScheduleTask(task));
    }

    private async Task<CliCommandExecutionResult> NextAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (!task.NextRunTime.HasValue && task.CanBeEnabled)
        {
            task.CalculateNextRunTime();
        }

        return CliCommandExecutionResult.Ok($"Next run for schedule task: {task.NextRunTime?.ToString("O", CultureInfo.InvariantCulture) ?? "none"}.", MapScheduleTask(task));
    }

    private async Task<(ScheduledTask? Task, CliCommandExecutionResult? Result)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.FindTaskAsync(
            taskId,
            "schedule",
            "Schedule",
            cancellationToken,
            async () => _tasks = (await _manageSchedule.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            task => task.Id);
    }

    private static CliCommandExecutionResult? ApplyScheduleOptions(ScheduledTask task, ScheduleCliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Interval))
        {
            if (!TryParseInterval(options.Interval, out var intervalValue, out var intervalUnit, out var error))
            {
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Invalid schedule interval.", [error]);
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
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Invalid schedule datetime.", [$"Could not parse --at value: {options.At}"]);
            }

            task.Type = ScheduleType.SpecificTime;
            task.ScheduledDateTime = scheduledAt;
            return null;
        }

        if (!string.IsNullOrWhiteSpace(options.Weekly))
        {
            if (!TryParseScheduleDays(options.Weekly, out var days, out var error))
            {
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Invalid weekly schedule days.", [error]);
            }

            if (!string.IsNullOrWhiteSpace(options.Time)
                && !TimeSpan.TryParse(options.Time, CultureInfo.InvariantCulture, out var weeklyTime))
            {
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Invalid weekly schedule time.", [$"Could not parse --time value: {options.Time}"]);
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
            var part = rawPart.ToLowerInvariant();
            var parsed = part switch
            {
                "mon" or "monday" => ScheduleDays.Monday,
                "tue" or "tuesday" => ScheduleDays.Tuesday,
                "wed" or "wednesday" => ScheduleDays.Wednesday,
                "thu" or "thursday" => ScheduleDays.Thursday,
                "fri" or "friday" => ScheduleDays.Friday,
                "sat" or "saturday" => ScheduleDays.Saturday,
                "sun" or "sunday" => ScheduleDays.Sunday,
                "weekdays" => ScheduleDays.Weekdays,
                "weekends" => ScheduleDays.Weekends,
                "everyday" or "daily" or "all" => ScheduleDays.EveryDay,
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
}
