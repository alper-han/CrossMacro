using System.Collections.Generic;
using System.Linq;

namespace CrossMacro.Cli.Services;

public sealed class DoctorReport
{
    public required IReadOnlyList<DoctorCheck> Checks { get; init; }

    public bool HasFailures => Checks.Any(x => x.Status == DoctorCheckStatus.Fail);

    public bool HasWarnings => Checks.Any(x => x.Status == DoctorCheckStatus.Warn);
}
