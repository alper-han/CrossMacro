
namespace CrossMacro.Cli.Serialization;

public sealed record class ScheduleTaskData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("playbackSpeed")] double PlaybackSpeed,
    [property: JsonPropertyName("intervalValue"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IntervalValue,
    [property: JsonPropertyName("intervalUnit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IntervalUnit,
    [property: JsonPropertyName("scheduledDateTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? ScheduledDateTime,
    [property: JsonPropertyName("weeklyDays"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyDays,
    [property: JsonPropertyName("weeklyTime"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WeeklyTime,
    [property: JsonPropertyName("nextRunTime")] DateTime? NextRunTime,
    [property: JsonPropertyName("lastRunTime")] DateTime? LastRunTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
