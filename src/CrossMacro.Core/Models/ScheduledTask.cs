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
    SpecificTime
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
    public string Name { get; set; } = "New Task";
    
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
            OnPropertyChanged(nameof(CanBeEnabled));
        }
    }
    
    /// <summary>
    /// Type of schedule (Interval or DateTime)
    /// </summary>
    public ScheduleType Type { get; set; } = ScheduleType.Interval;
    
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
            // Can only enable if macro file is set
            if (value && string.IsNullOrEmpty(MacroFilePath))
            {
                return; // Don't allow enabling without a macro file
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
    public bool CanBeEnabled => !string.IsNullOrEmpty(MacroFilePath);
    
    // Interval settings
    
    /// <summary>
    /// Interval value (used with IntervalUnit)
    /// </summary>
    public int IntervalValue { get; set; } = 30;
    
    /// <summary>
    /// Unit for the interval (Seconds, Minutes, Hours)
    /// </summary>
    public IntervalUnit IntervalUnit { get; set; } = IntervalUnit.Seconds;
    
    // DateTime settings
    
    /// <summary>
    /// Scheduled date and time for DateTime type
    /// </summary>
    public DateTime? ScheduledDateTime { get; set; }
    
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
    /// Calculates the interval in milliseconds
    /// </summary>
    public int GetIntervalMs()
    {
        return IntervalUnit switch
        {
            IntervalUnit.Seconds => IntervalValue * 1000,
            IntervalUnit.Minutes => IntervalValue * 60 * 1000,
            IntervalUnit.Hours => IntervalValue * 60 * 60 * 1000,
            _ => IntervalValue * 1000
        };
    }
    
    /// <summary>
    /// Calculates the next run time based on schedule type
    /// </summary>
    public void CalculateNextRunTime(DateTime? now = null)
    {
        var baseTime = now ?? DateTime.UtcNow;
        if (Type == ScheduleType.Interval)
        {
            NextRunTime = baseTime.AddMilliseconds(GetIntervalMs());
        }
        else if (Type == ScheduleType.SpecificTime && ScheduledDateTime.HasValue)
        {
            // Ensure comparison uses UTC
            var scheduledUtc = ScheduledDateTime.Value.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(ScheduledDateTime.Value, DateTimeKind.Local).ToUniversalTime() 
                : ScheduledDateTime.Value.ToUniversalTime();

            NextRunTime = scheduledUtc;
        }
    }
}
