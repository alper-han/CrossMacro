using System.Text.Json.Serialization;

namespace CrossMacro.Cli.Serialization;

public sealed record MacroSummaryData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("macroName")] string MacroName,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("isAbsoluteCoordinates")] bool IsAbsoluteCoordinates
);
