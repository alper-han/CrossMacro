using System;
using System.Collections.Generic;
using System.IO;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxDaemonSocketAccessStatus
{
    Accessible = 0,
    Missing = 1,
    PermissionDenied = 2,
    WrongType = 3,
    ConnectionRefusedOrStale = 4,
    Timeout = 5,
    UnexpectedError = 6
}

public enum LinuxDaemonHandshakeStatus
{
    Success = 0,
    MissingSocket = 1,
    PermissionDenied = 2,
    WrongSocketType = 3,
    ConnectionRefusedOrStale = 4,
    Timeout = 5,
    ProtocolMismatch = 6,
    HandshakeRejected = 7,
    UnexpectedError = 8
}

public enum LinuxDaemonGroupMembershipStatus
{
    Member = 0,
    MissingGroup = 1,
    UserNotMember = 2,
    StaleSession = 3,
    Unknown = 4
}

public enum LinuxDirectInputFallbackStatus
{
    Available = 0,
    MissingUInputWriteAccess = 1,
    MissingInputEventReadAccess = 2,
    Unavailable = 3,
    Unknown = 4
}

public enum LinuxFileSystemEntryKind
{
    Unknown = 0,
    Missing = 1,
    Socket = 2,
    Directory = 3,
    File = 4,
    Other = 5
}

public readonly record struct LinuxDaemonSocketProbeOptions(
    string SocketPath,
    string RequiredGroupName);

public readonly record struct LinuxDaemonSocketMetadata(
    string Path,
    LinuxFileSystemEntryKind EntryKind,
    int? OwnerUserId = null,
    int? OwnerGroupId = null,
    UnixFileMode? Permissions = null);

public sealed record LinuxDaemonGroupDefinition(
    string Name,
    int GroupId,
    IReadOnlyCollection<string> MemberNames);

public sealed record LinuxDaemonCurrentUserGroups(
    int UserId,
    string UserName,
    int PrimaryGroupId,
    IReadOnlyCollection<int> SupplementaryGroupIds);

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
    public bool IsCurrentSessionMember => Status == LinuxDaemonGroupMembershipStatus.Member;
}

public interface ILinuxDaemonSocketAccessProbe
{
    LinuxDaemonSocketAccessResult Probe(LinuxDaemonSocketProbeOptions options);
}

public readonly record struct LinuxDaemonSocketAccessResult(
    string SocketPath,
    LinuxDaemonSocketAccessStatus Status,
    LinuxDaemonGroupMembershipStatus GroupMembershipStatus = LinuxDaemonGroupMembershipStatus.Unknown,
    LinuxDaemonSocketMetadata? Metadata = null,
    LinuxDaemonGroupMembershipResult? GroupMembership = null,
    string? Message = null,
    Exception? Exception = null)
{
    public bool IsAccessible => Status == LinuxDaemonSocketAccessStatus.Accessible;

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

public readonly record struct LinuxDaemonHandshakeProbeResult(
    string SocketPath,
    LinuxDaemonHandshakeStatus Status,
    TimeSpan Timeout,
    string? Message = null,
    Exception? Exception = null)
{
    public bool Succeeded => Status == LinuxDaemonHandshakeStatus.Success;
    public bool TimedOut => Status == LinuxDaemonHandshakeStatus.Timeout;

    public static LinuxDaemonHandshakeProbeResult Success(string socketPath, TimeSpan timeout)
    {
        return new(socketPath, LinuxDaemonHandshakeStatus.Success, timeout);
    }

    public static LinuxDaemonHandshakeProbeResult Failed(
        string socketPath,
        TimeSpan timeout,
        LinuxDaemonHandshakeStatus status,
        string? message = null,
        Exception? exception = null)
    {
        if (status == LinuxDaemonHandshakeStatus.Success)
        {
            throw new ArgumentException("Use Success for successful daemon handshakes.", nameof(status));
        }

        return new(socketPath, status, timeout, message, exception);
    }
}

public readonly record struct LinuxDirectInputFallbackResult(
    LinuxDirectInputFallbackStatus Status,
    bool CanWriteUInput,
    bool CanReadInputEvents,
    string? Message = null)
{
    public bool IsAvailable => Status == LinuxDirectInputFallbackStatus.Available;

    public static LinuxDirectInputFallbackResult FromAccess(bool canWriteUInput, bool canReadInputEvents)
    {
        if (canWriteUInput && canReadInputEvents)
        {
            return new(LinuxDirectInputFallbackStatus.Available, true, true);
        }

        var status = !canWriteUInput
            ? LinuxDirectInputFallbackStatus.MissingUInputWriteAccess
            : LinuxDirectInputFallbackStatus.MissingInputEventReadAccess;

        return new(status, canWriteUInput, canReadInputEvents);
    }
}

public readonly record struct LinuxDaemonDiagnosticSnapshot(
    string SocketPath,
    LinuxDaemonSocketAccessResult SocketAccess,
    LinuxDaemonGroupMembershipStatus GroupMembershipStatus,
    LinuxDirectInputFallbackResult DirectInputFallback,
    LinuxDaemonHandshakeProbeResult Handshake)
{
    public bool CanUseDaemon => SocketAccess.IsAccessible && Handshake.Succeeded;
}
