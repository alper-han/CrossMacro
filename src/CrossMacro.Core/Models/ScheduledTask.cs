using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossMacro.Core.Models;

/// <summary>
/// Type of schedule for a scheduled task
/// </summary>
public enum ScheduleType
{
    /// <summary>
    /// Repeats at regular intervals (seconds, minutes, hours)
    /// </summary>
    Interval,
    
    /// <summary>
    /// Runs once at a specific date and time
    /// </summary>
    SpecificTime,

    /// <summary>
    /// Repeats weekly on selected days at a specific local time
    /// </summary>
    Weekly
}

/// <summary>
/// Days of week used by weekly scheduled tasks.
/// </summary>
[Flags]
public enum ScheduleDays
{
    None = 0,
    Monday = 1 << 0,
    Tuesday = 1 << 1,
    Wednesday = 1 << 2,
    Thursday = 1 << 3,
    Friday = 1 << 4,
    Saturday = 1 << 5,
    Sunday = 1 << 6,

    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekends = Saturday | Sunday,
    EveryDay = Weekdays | Weekends
}

/// <summary>
/// Unit of time for interval-based scheduling
/// </summary>
public enum IntervalUnit
{
    Seconds,
    Minutes,
    Hours
}

/// <summary>
/// Represents a scheduled macro task
/// </summary>
public class ScheduledTask : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    /// <summary>
    /// Unique identifier for this scheduled task
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Display name for the task
    /// </summary>
    private string _name = "New Task";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Path to the macro file to execute
    /// </summary>
    private string _macroFilePath = string.Empty;
    public string MacroFilePath 
    { 
        get => _macroFilePath;
        set 
        { 
            _macroFilePath = value; 
            OnPropertyChanged(); 
            RefreshEnableState();
        }
    }
    
    /// <summary>
    /// Type of schedule (Interval, DateTime, or Weekly)
    /// </summary>
    private ScheduleType _type = ScheduleType.Interval;
    public ScheduleType Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged();
            RefreshEnableState();
        }
    }
    
    /// <summary>
    /// Playback speed multiplier (0.1 = 10x slower, 1.0 = normal, 10.0 = 10x faster)
    /// </summary>
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed 
    { 
        get => _playbackSpeed;
        set
        {
            var normalized = PlaybackOptions.NormalizeSpeedMultiplier(value);
            if (Math.Abs(_playbackSpeed - normalized) > double.Epsilon)
            {
                _playbackSpeed = normalized;
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// Whether the task is enabled
    /// </summary>
    private bool _isEnabled;
    public bool IsEnabled 
    { 
        get => _isEnabled;
        set
        {
            // Can only enable if required schedule inputs are set
            if (value && !CanBeEnabled)
            {
                return; // Don't allow enabling without required inputs
            }
            
            _isEnabled = value;
            OnPropertyChanged();
            if (value)
            {
                CalculateNextRunTime();
            }
            else
            {
                NextRunTime = null;
            }
        }
    }
    
    /// <summary>
    /// Whether the task can be enabled (has a macro file path)
    /// </summary>
    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath)
        && (Type != ScheduleType.Weekly || WeeklyDays != ScheduleDays.None);
    
    // Interval settings
    
    /// <summary>
    /// Interval value (used with IntervalUnit)
    /// </summary>
    private int _intervalValue = 30;
    public int IntervalValue
    {
        get => _intervalValue;
        set { _intervalValue = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Unit for the interval (Seconds, Minutes, Hours)
    /// </summary>
    private IntervalUnit _intervalUnit = IntervalUnit.Seconds;
    public IntervalUnit IntervalUnit
    {
        get => _intervalUnit;
        set { _intervalUnit = value; OnPropertyChanged(); }
    }

    private bool _useRandomIntervalDelay;
    public bool UseRandomIntervalDelay
    {
        get => _useRandomIntervalDelay;
        set { _useRandomIntervalDelay = value; OnPropertyChanged(); }
    }

    private int _intervalMinValue = 1;
    public int IntervalMinValue
    {
        get => _intervalMinValue;
        set
        {
            var (min, max) = NormalizeIntervalRange(value, _intervalMaxValue);
            _intervalMinValue = min;
            _intervalMaxValue = max;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IntervalMaxValue));
        }
    }

    private int _intervalMaxValue = 30;
    public int IntervalMaxValue
    {
        get => _intervalMaxValue;
        set
        {
            var (min, max) = NormalizeIntervalRange(_intervalMinValue, value);
            _intervalMinValue = min;
            _intervalMaxValue = max;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IntervalMinValue));
        }
    }
    
    // DateTime settings
    
    /// <summary>
    /// Scheduled date and time for DateTime type
    /// </summary>
    private DateTime? _scheduledDateTime;
    public DateTime? ScheduledDateTime
    {
        get => _scheduledDateTime;
        set { _scheduledDateTime = value; OnPropertyChanged(); }
    }

    // Weekly settings

    /// <summary>
    /// Days on which a weekly task should run.
    /// </summary>
    private ScheduleDays _weeklyDays = ScheduleDays.Weekdays;
    public ScheduleDays WeeklyDays
    {
        get => _weeklyDays;
        set
        {
            _weeklyDays = value;
            OnPropertyChanged();
            RefreshEnableState();
        }
    }

    private void RefreshEnableState()
    {
        OnPropertyChanged(nameof(CanBeEnabled));
        if (_isEnabled && !CanBeEnabled)
        {
            _isEnabled = false;
            OnPropertyChanged(nameof(IsEnabled));
            NextRunTime = null;
        }
    }

    /// <summary>
    /// Local time of day at which a weekly task should run.
    /// </summary>
    private TimeSpan _weeklyTime = new(9, 0, 0);
    public TimeSpan WeeklyTime
    {
        get => _weeklyTime;
        set { _weeklyTime = NormalizeTimeOfDay(value); OnPropertyChanged(); }
    }
    
    // State
    
    /// <summary>
    /// When the task was last executed
    /// </summary>
    private DateTime? _lastRunTime;
    public DateTime? LastRunTime 
    { 
        get => _lastRunTime;
        set { _lastRunTime = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// When the task is scheduled to run next
    /// </summary>
    private DateTime? _nextRunTime;
    public DateTime? NextRunTime 
    { 
        get => _nextRunTime;
        set { _nextRunTime = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Status message from last execution
    /// </summary>
    private string? _lastStatus;
    public string? LastStatus 
    { 
        get => _lastStatus;
        set { _lastStatus = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Calculates the interval as <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan GetInterval()
    {
        return GetIntervalForValue(IntervalValue);
    }

    private TimeSpan GetIntervalForValue(long intervalValue)
    {
        var normalizedIntervalValue = Math.Max(1, intervalValue);
        var ticksPerUnit = IntervalUnit switch
        {
            IntervalUnit.Seconds => TimeSpan.TicksPerSecond,
            IntervalUnit.Minutes => TimeSpan.TicksPerMinute,
            IntervalUnit.Hours => TimeSpan.TicksPerHour,
            _ => TimeSpan.TicksPerSecond
        };

        long totalTicks;
        try
        {
            totalTicks = checked(normalizedIntervalValue * ticksPerUnit);
        }
        catch (OverflowException)
        {
            totalTicks = TimeSpan.MaxValue.Ticks;
        }

        if (totalTicks > TimeSpan.MaxValue.Ticks)
        {
            totalTicks = TimeSpan.MaxValue.Ticks;
        }

        return TimeSpan.FromTicks(totalTicks);
    }

    /// <summary>
    /// Calculates the interval in milliseconds.
    /// </summary>
    public int GetIntervalMs()
    {
        var interval = GetInterval();
        var maxIntMilliseconds = TimeSpan.FromMilliseconds(int.MaxValue);

        if (interval >= maxIntMilliseconds)
        {
            return int.MaxValue;
        }

        return (int)interval.TotalMilliseconds;
    }
    
    /// <summary>
    /// Calculates the next run time based on schedule type
    /// </summary>
    public void CalculateNextRunTime(DateTime? now = null)
    {
        var baseTime = now ?? DateTime.UtcNow;
        if (Type == ScheduleType.Interval)
        {
            NextRunTime = AddIntervalClamped(baseTime, GetNextIntervalDelay());
        }
        else if (Type == ScheduleType.SpecificTime && ScheduledDateTime.HasValue)
        {
            // Ensure comparison uses UTC
            var scheduledUtc = ScheduledDateTime.Value.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(ScheduledDateTime.Value, DateTimeKind.Local).ToUniversalTime() 
                : ScheduledDateTime.Value.ToUniversalTime();

            NextRunTime = scheduledUtc;
        }
        else if (Type == ScheduleType.Weekly)
        {
            NextRunTime = CalculateNextWeeklyRunTime(baseTime);
        }
    }

    private DateTime? CalculateNextWeeklyRunTime(DateTime baseTime)
    {
        if (WeeklyDays == ScheduleDays.None)
        {
            return null;
        }

        var localBaseTime = baseTime.Kind switch
        {
            DateTimeKind.Local => baseTime,
            DateTimeKind.Utc => baseTime.ToLocalTime(),
            _ => DateTime.SpecifyKind(baseTime, DateTimeKind.Local)
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

    private static ScheduleDays ToScheduleDay(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => ScheduleDays.Monday,
            DayOfWeek.Tuesday => ScheduleDays.Tuesday,
            DayOfWeek.Wednesday => ScheduleDays.Wednesday,
            DayOfWeek.Thursday => ScheduleDays.Thursday,
            DayOfWeek.Friday => ScheduleDays.Friday,
            DayOfWeek.Saturday => ScheduleDays.Saturday,
            DayOfWeek.Sunday => ScheduleDays.Sunday,
            _ => ScheduleDays.None
        };
    }

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

    private TimeSpan GetNextIntervalDelay()
    {
        if (!UseRandomIntervalDelay)
        {
            return GetInterval();
        }

        var (min, max) = NormalizeIntervalRange(IntervalMinValue, IntervalMaxValue);
        var intervalValue = min == max ? min : Random.Shared.NextInt64(min, (long)max + 1);
        return GetIntervalForValue(intervalValue);
    }

    private static (int Min, int Max) NormalizeIntervalRange(int min, int max)
    {
        min = Math.Max(1, min);
        max = Math.Max(1, max);

        if (max < min)
        {
            max = min;
        }

        return (min, max);
    }

    private static DateTime AddIntervalClamped(DateTime baseTime, TimeSpan interval)
    {
        try
        {
            return baseTime + interval;
        }
        catch (ArgumentOutOfRangeException)
        {
            return interval >= TimeSpan.Zero
                ? new DateTime(DateTime.MaxValue.Ticks, baseTime.Kind)
                : new DateTime(DateTime.MinValue.Ticks, baseTime.Kind);
        }
    }
}
