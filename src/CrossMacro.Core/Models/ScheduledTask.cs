namespace CrossMacro.Core.Models;

/// <summary>Persisted state and runtime schedule inputs for a macro task.</summary>
public class ScheduledTask
{
    private int _intervalMinValue = 1;
    private int _intervalMaxValue = 30;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Task";
    public string MacroFilePath { get; set; } = string.Empty;

    public ScheduleType Type
    {
        get;
        set
        {
            field = value;
            MaintainEnablementInvariant();
        }
    } = ScheduleType.Interval;

    public double PlaybackSpeed { get; set; } = PlaybackOptions.DefaultSpeedMultiplier;

    public bool IsEnabled
    {
        get;
        set
        {
            if (value && !CanBeEnabled)
            {
                field = false;
                NextRunTime = null;
                return;
            }

            field = value;
            if (!value)
            {
                NextRunTime = null;
                return;
            }

            CalculateNextRunTime();
        }
    }

    public int IntervalValue { get; set; } = 30;
    public IntervalUnit IntervalUnit { get; set; } = IntervalUnit.Seconds;
    public bool UseRandomIntervalDelay { get; set; }

    public int IntervalMinValue
    {
        get => _intervalMinValue;
        set
        {
            var normalized = NormalizeIntervalRange(value, _intervalMaxValue);
            _intervalMinValue = normalized.Min;
            _intervalMaxValue = normalized.Max;
        }
    }

    public int IntervalMaxValue
    {
        get => _intervalMaxValue;
        set
        {
            var normalized = NormalizeIntervalRange(_intervalMinValue, value);
            _intervalMinValue = normalized.Min;
            _intervalMaxValue = normalized.Max;
        }
    }

    public DateTime? ScheduledDateTime { get; set; }

    public ScheduleDays WeeklyDays
    {
        get;
        set
        {
            field = value;
            MaintainEnablementInvariant();
        }
    } = ScheduleDays.Weekdays;

    public TimeSpan WeeklyTime { get; set; } = new(9, 0, 0);
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string? LastStatus { get; set; }

    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath)
        && (Type is not ScheduleType.Weekly || WeeklyDays is not ScheduleDays.None);

    /// <summary>
    /// Normalizes scalar fields that lack setter-level guards and re-checks the
    /// enablement invariant. Required after bulk load (deserialization, DB read,
    /// or migration) before the task is used at runtime.
    /// </summary>
    public void Normalize()
    {
        PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(PlaybackSpeed);
        IntervalValue = Math.Max(1, IntervalValue);
        WeeklyTime = NormalizeTimeOfDay(WeeklyTime);
        // Interval pair is already guarded by its setters; re-normalize in case
        // the backing fields were bypassed (e.g. reflection-based deserialization).
        (IntervalMinValue, IntervalMaxValue) = NormalizeIntervalRange(IntervalMinValue, IntervalMaxValue);
        MaintainEnablementInvariant();
    }

    /// <summary>
    /// Attempts to enable/disable the task, returning <langword>false</langword>
    /// when the task cannot be enabled (e.g. weekly task with no selected days).
    /// Equivalent to <see cref="IsEnabled"/> setter but reports the rejection
    /// instead of silently leaving the task disabled.
    /// </summary>
    public bool TrySetEnabled(bool enabled)
    {
        if (enabled && !CanBeEnabled)
        {
            IsEnabled = false;
            return false;
        }

        IsEnabled = enabled;
        return true;
    }

    private void MaintainEnablementInvariant()
    {
        if (IsEnabled && !CanBeEnabled)
        {
            IsEnabled = false;
        }
    }

    public TimeSpan GetInterval() => GetIntervalForValue(IntervalValue);

    public int GetIntervalMs()
    {
        var interval = GetInterval();
        var maxIntMilliseconds = TimeSpan.FromMilliseconds(int.MaxValue);
        return interval >= maxIntMilliseconds ? int.MaxValue : Convert.ToInt32(Math.Truncate(interval.TotalMilliseconds));
    }

    public void CalculateNextRunTime(DateTime? now = null)
    {
        var baseTime = now ?? DateTime.UtcNow;
        if (Type is ScheduleType.Interval)
        {
            NextRunTime = AddIntervalClamped(baseTime, GetNextIntervalDelay());
        }
        else if (Type is ScheduleType.SpecificTime && ScheduledDateTime is not null)
        {
            NextRunTime = ScheduledDateTime.Value.Kind is DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(ScheduledDateTime.Value, DateTimeKind.Local).ToUniversalTime()
                : ScheduledDateTime.Value.ToUniversalTime();
        }
        else if (Type is ScheduleType.Weekly)
        {
            NextRunTime = CalculateNextWeeklyRunTime(baseTime);
        }
        else
        {
            NextRunTime = null;
        }
    }

    private TimeSpan GetIntervalForValue(long intervalValue)
    {
        var normalizedIntervalValue = Math.Max(1, intervalValue);
        var ticksPerUnit = IntervalUnit switch
        {
            IntervalUnit.Seconds => TimeSpan.TicksPerSecond,
            IntervalUnit.Minutes => TimeSpan.TicksPerMinute,
            IntervalUnit.Hours => TimeSpan.TicksPerHour,
            _ => TimeSpan.TicksPerSecond,
        };
        long totalTicks;
        try { totalTicks = checked(normalizedIntervalValue * ticksPerUnit); }
        catch (OverflowException) { totalTicks = TimeSpan.MaxValue.Ticks; }
        return TimeSpan.FromTicks(Math.Min(totalTicks, TimeSpan.MaxValue.Ticks));
    }

    private DateTime? CalculateNextWeeklyRunTime(DateTime baseTime)
    {
        if (WeeklyDays is ScheduleDays.None)
        {
            return null;
        }

        var localBaseTime = baseTime.Kind switch
        {
            DateTimeKind.Local => baseTime,
            DateTimeKind.Utc => baseTime.ToLocalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(baseTime, DateTimeKind.Local),
            _ => DateTime.SpecifyKind(baseTime, DateTimeKind.Local),
        };
        var normalizedWeeklyTime = NormalizeTimeOfDay(WeeklyTime);
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var candidateDate = localBaseTime.Date.AddDays(dayOffset);
            if (!WeeklyDays.HasFlag(ToScheduleDay(candidateDate.DayOfWeek)))
            {
                continue;
            }

            var candidateLocal = DateTime.SpecifyKind(candidateDate + normalizedWeeklyTime, DateTimeKind.Local);
            if (candidateLocal > localBaseTime)
            {
                return candidateLocal.ToUniversalTime();
            }
        }
        return null;
    }

    private TimeSpan GetNextIntervalDelay()
    {
        if (!UseRandomIntervalDelay)
        {
            return GetInterval();
        }

        var (min, max) = NormalizeIntervalRange(IntervalMinValue, IntervalMaxValue);
        var intervalValue = min == max ? min : RandomNumberGeneratorUtility.GetInt32Inclusive(min, max);
        return GetIntervalForValue(intervalValue);
    }

    private static (int Min, int Max) NormalizeIntervalRange(int min, int max)
    {
        min = Math.Max(1, min);
        max = Math.Max(1, max);
        return (min, Math.Max(min, max));
    }

    private static ScheduleDays ToScheduleDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => ScheduleDays.Monday,
        DayOfWeek.Tuesday => ScheduleDays.Tuesday,
        DayOfWeek.Wednesday => ScheduleDays.Wednesday,
        DayOfWeek.Thursday => ScheduleDays.Thursday,
        DayOfWeek.Friday => ScheduleDays.Friday,
        DayOfWeek.Saturday => ScheduleDays.Saturday,
        DayOfWeek.Sunday => ScheduleDays.Sunday,
        _ => ScheduleDays.None,
    };

    private static TimeSpan NormalizeTimeOfDay(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return time >= TimeSpan.FromDays(1)
            ? TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1)
            : time;
    }

    private static DateTime AddIntervalClamped(DateTime baseTime, TimeSpan interval)
    {
        try { return baseTime + interval; }
        catch (ArgumentOutOfRangeException)
        {
            return new DateTime(interval >= TimeSpan.Zero ? DateTime.MaxValue.Ticks : DateTime.MinValue.Ticks, baseTime.Kind);
        }
    }
}
