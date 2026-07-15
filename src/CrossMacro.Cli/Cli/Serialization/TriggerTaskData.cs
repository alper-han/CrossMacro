
namespace CrossMacro.Cli.Serialization;

public sealed record TriggerTaskData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("matchMode")] string MatchMode,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("targetProfileId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetProfileId,
    [property: JsonPropertyName("macroFilePath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MacroFilePath,
    [property: JsonPropertyName("fireMode")] string FireMode,
    [property: JsonPropertyName("cooldownMs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CooldownMs,
    [property: JsonPropertyName("debounceMs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DebounceMs,
    [property: JsonPropertyName("lastTriggeredTime")] DateTime? LastTriggeredTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
