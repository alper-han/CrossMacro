
namespace CrossMacro.Cli.Serialization;

public sealed record class DoctorCheckOutput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] JsonNode? Details
);
