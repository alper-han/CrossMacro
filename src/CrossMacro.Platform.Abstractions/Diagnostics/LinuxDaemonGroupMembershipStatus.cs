namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxDaemonGroupMembershipStatus
{
    Member = 0,
    MissingGroup = 1,
    UserNotMember = 2,
    StaleSession = 3,
    Unknown = 4,
}
