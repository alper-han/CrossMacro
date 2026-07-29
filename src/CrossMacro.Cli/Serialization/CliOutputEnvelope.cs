
namespace CrossMacro.Cli.Serialization;

public sealed record CliOutputEnvelope(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] JsonNode? Data,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors
);
