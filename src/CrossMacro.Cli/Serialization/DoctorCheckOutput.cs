
namespace CrossMacro.Cli.Serialization;

public sealed record DoctorCheckOutput(
    string Name,
    string Status,
    string Message,
    JsonNode? Details
);
