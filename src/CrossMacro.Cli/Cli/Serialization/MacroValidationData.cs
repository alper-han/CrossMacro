
namespace CrossMacro.Cli.Serialization;

public sealed record class MacroValidationData(
    [property: JsonPropertyName("macroPath")] string MacroPath,
    [property: JsonPropertyName("eventCount")] int EventCount
);
