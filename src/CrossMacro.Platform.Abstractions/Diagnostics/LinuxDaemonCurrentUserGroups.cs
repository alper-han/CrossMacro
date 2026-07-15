using System.Collections.Generic;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record LinuxDaemonCurrentUserGroups(
    int UserId,
    string UserName,
    int PrimaryGroupId,
    IReadOnlyCollection<int> SupplementaryGroupIds);
