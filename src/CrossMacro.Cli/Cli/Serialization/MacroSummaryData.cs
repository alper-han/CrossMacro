
namespace CrossMacro.Cli.Serialization;

public sealed record class MacroSummaryData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("macroName")] string MacroName,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("isAbsoluteCoordinates")] bool IsAbsoluteCoordinates
);
