
namespace CrossMacro.Cli.Serialization;

public sealed record ScheduleTaskData(
    Guid Id,
    string Name,
    bool Enabled,
    string Type,
    string MacroFilePath,
    double PlaybackSpeed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IntervalValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IntervalUnit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? ScheduledDateTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyDays,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyTime,
    DateTime? NextRunTime,
    DateTime? LastRunTime,
    string? LastStatus
);
