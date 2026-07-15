
namespace CrossMacro.Cli.Serialization;

public sealed record ShortcutTaskData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("hotkey")] string Hotkey,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("playbackSpeed")] double PlaybackSpeed,
    [property: JsonPropertyName("loopEnabled")] bool LoopEnabled,
    [property: JsonPropertyName("runWhileHeld")] bool RunWhileHeld,
    [property: JsonPropertyName("repeatCount")] int RepeatCount,
    [property: JsonPropertyName("repeatDelayMs")] int RepeatDelayMs,
    [property: JsonPropertyName("randomRepeatDelay")] bool RandomRepeatDelay,
    [property: JsonPropertyName("repeatDelayMinMs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RepeatDelayMinMs,
    [property: JsonPropertyName("repeatDelayMaxMs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RepeatDelayMaxMs,
    [property: JsonPropertyName("lastTriggeredTime")] DateTime? LastTriggeredTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
