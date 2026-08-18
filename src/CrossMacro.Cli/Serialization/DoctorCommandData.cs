
namespace CrossMacro.Cli.Serialization;

public sealed record DoctorCommandData(
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheckOutput> Checks
);
