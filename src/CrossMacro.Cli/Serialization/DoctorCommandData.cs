
namespace CrossMacro.Cli.Serialization;

public sealed record DoctorCommandData(
    IReadOnlyList<DoctorCheckOutput> Checks
);
