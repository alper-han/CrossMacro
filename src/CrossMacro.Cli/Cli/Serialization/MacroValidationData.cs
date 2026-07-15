
namespace CrossMacro.Cli.Serialization;

public sealed record MacroValidationData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("eventCount")] int EventCount
);
