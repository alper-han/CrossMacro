using System;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonSocketAccessResult(
    string SocketPath,
    LinuxDaemonSocketAccessStatus Status,
    LinuxDaemonGroupMembershipStatus GroupMembershipStatus = LinuxDaemonGroupMembershipStatus.Unknown,
    LinuxDaemonSocketMetadata? Metadata = null,
    LinuxDaemonGroupMembershipResult? GroupMembership = null,
    string? Message = null,
    Exception? Exception = null)
{
    public bool IsAccessible => Status is LinuxDaemonSocketAccessStatus.Accessible;

    public static LinuxDaemonSocketAccessResult Accessible(
        string socketPath,
        LinuxDaemonGroupMembershipStatus groupMembershipStatus = LinuxDaemonGroupMembershipStatus.Unknown)
    {
        return new(socketPath, LinuxDaemonSocketAccessStatus.Accessible, groupMembershipStatus);
    }

    public static LinuxDaemonSocketAccessResult Missing(string socketPath)
    {
        return new(socketPath, LinuxDaemonSocketAccessStatus.Missing);
    }
}
