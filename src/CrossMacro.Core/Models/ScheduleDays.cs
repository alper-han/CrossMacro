namespace CrossMacro.Core.Models;

/// <summary>
/// Days of week used by weekly scheduled tasks.
/// </summary>
[System.Flags]
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
    EveryDay = Weekdays | Weekends,
}
