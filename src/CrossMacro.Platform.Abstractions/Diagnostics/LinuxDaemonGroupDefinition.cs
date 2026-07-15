using System.Collections.Generic;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record LinuxDaemonGroupDefinition(
    string Name,
    int GroupId,
    IReadOnlyCollection<string> MemberNames);
