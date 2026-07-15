using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CrossMacro.Cli.Serialization;

public sealed record DoctorCommandData(
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheckOutput> Checks
);
