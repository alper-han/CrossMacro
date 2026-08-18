
namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonGroupMembershipResult(
    string GroupName,
    LinuxDaemonGroupMembershipStatus Status,
    int? GroupId = null,
    string? UserName = null,
    int? UserId = null,
    IReadOnlyCollection<int>? CurrentProcessGroupIds = null,
    string? Message = null,
    Exception? Exception = null)
{
    public bool IsCurrentSessionMember => Status is LinuxDaemonGroupMembershipStatus.Member;
}
