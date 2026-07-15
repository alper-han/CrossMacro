
namespace CrossMacro.Cli.Serialization;

public sealed record class MacroInfoData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("macroName")] string MacroName,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("isAbsoluteCoordinates")] bool IsAbsoluteCoordinates,
    [property: JsonPropertyName("skipInitialZeroZero")] bool SkipInitialZeroZero,
    [property: JsonPropertyName("trailingDelayMs")] int TrailingDelayMs,
    [property: JsonPropertyName("hasTrailingRandomDelay")] bool HasTrailingRandomDelay,
    [property: JsonPropertyName("trailingDelayMinMs")] int TrailingDelayMinMs,
    [property: JsonPropertyName("trailingDelayMaxMs")] int TrailingDelayMaxMs,
    [property: JsonPropertyName("eventBreakdown")] MacroEventBreakdownData EventBreakdown
);
