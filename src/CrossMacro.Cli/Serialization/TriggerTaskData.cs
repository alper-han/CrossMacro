
namespace CrossMacro.Cli.Serialization;

public sealed record TriggerTaskData(
    Guid Id,
    string Name,
    bool Enabled,
    string Field,
    string MatchMode,
    string Value,
    string Action,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetProfileId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MacroFilePath,
    string FireMode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CooldownMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DebounceMs,
    DateTime? LastTriggeredTime,
    string? LastStatus
);
