
namespace CrossMacro.Cli.Serialization;

public sealed record class HeadlessRuntimeData(
    [property: JsonPropertyName("globalHotkeys")] bool GlobalHotkeys,
    [property: JsonPropertyName("scheduler")] bool Scheduler,
    [property: JsonPropertyName("shortcuts")] bool Shortcuts,
    [property: JsonPropertyName("textExpansion")] bool TextExpansion,
    [property: JsonPropertyName("hotkeyActions")] bool HotkeyActions
);
