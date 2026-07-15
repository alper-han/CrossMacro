
namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record class LinuxDaemonCurrentUserGroups(
    int UserId,
    string UserName,
    int PrimaryGroupId,
    IReadOnlyCollection<int> SupplementaryGroupIds);
