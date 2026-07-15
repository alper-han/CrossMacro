
namespace CrossMacro.Cli.Serialization;

public sealed record class RunScriptExecutionData(
    [property: JsonPropertyName("stepCount")] int StepCount,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("totalDurationMs")] long TotalDurationMs,
    [property: JsonPropertyName("initialDelayMs")] int InitialDelayMs,
    [property: JsonPropertyName("initialHasRandomDelay")] bool InitialHasRandomDelay,
    [property: JsonPropertyName("initialRandomDelayMinMs")] int InitialRandomDelayMinMs,
    [property: JsonPropertyName("initialRandomDelayMaxMs")] int InitialRandomDelayMaxMs,
    [property: JsonPropertyName("trailingDelayMs")] int TrailingDelayMs,
    [property: JsonPropertyName("coordinateMode")] string CoordinateMode,
    [property: JsonPropertyName("runtimeVariables")] IReadOnlyDictionary<string, string> RuntimeVariables
);
