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
    Weekly,
}
