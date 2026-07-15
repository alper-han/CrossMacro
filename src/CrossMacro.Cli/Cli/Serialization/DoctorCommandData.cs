
namespace CrossMacro.Cli.Serialization;

public sealed record class DoctorCommandData(
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheckOutput> Checks
);
