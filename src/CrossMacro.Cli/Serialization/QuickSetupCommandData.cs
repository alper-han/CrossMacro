namespace CrossMacro.Cli.Serialization;

public sealed record QuickSetupCommandData(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("applicable")] bool Applicable,
    [property: JsonPropertyName("applied")] bool Applied);
