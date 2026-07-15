
namespace CrossMacro.Cli.Serialization;

public sealed record RecordExecutionData(
    [property: JsonPropertyName("outputPath")] string OutputPath,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("recordMouse")] bool RecordMouse,
    [property: JsonPropertyName("recordKeyboard")] bool RecordKeyboard,
    [property: JsonPropertyName("requestedMode")] string RequestedMode,
    [property: JsonPropertyName("actualMode")] string ActualMode,
    [property: JsonPropertyName("skipInitialZero")] bool SkipInitialZero
);
