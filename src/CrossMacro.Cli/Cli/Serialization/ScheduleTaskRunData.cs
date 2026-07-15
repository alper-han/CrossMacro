
namespace CrossMacro.Cli.Serialization;

public sealed record ScheduleTaskRunData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("lastRunTime")] DateTime? LastRunTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
