
namespace CrossMacro.Cli.Serialization;

public sealed record ShortcutTaskRunData(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("hotkey")] string Hotkey,
    [property: JsonPropertyName("macroFilePath")] string MacroFilePath,
    [property: JsonPropertyName("lastTriggeredTime")] DateTime? LastTriggeredTime,
    [property: JsonPropertyName("lastStatus")] string? LastStatus
);
