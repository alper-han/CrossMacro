using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.Ipc;

namespace CrossMacro.Platform.Linux.Services;

internal static class LinuxInputProbeUtilities
{
    internal static string? ResolveAvailableSocketPath(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        return fileExists(IpcProtocol.DefaultSocketPath)
            ? IpcProtocol.DefaultSocketPath
            : null;
    }

    internal static bool HasUInputWriteAccess(Func<string, bool> canOpenForWrite)
    {
        ArgumentNullException.ThrowIfNull(canOpenForWrite);

        return canOpenForWrite(LinuxConstants.UInputDevicePath) ||
               canOpenForWrite(LinuxConstants.UInputAlternatePath);
    }

    internal static bool HasReadableInputEventAccess(
        Func<string, bool> canOpenForRead,
        Func<string[]> getInputEventCandidates)
    {
        ArgumentNullException.ThrowIfNull(canOpenForRead);
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);

        var eventDevices = getInputEventCandidates();
        if (eventDevices.Length == 0)
        {
            return false;
        }

        return eventDevices.Any(canOpenForRead);
    }

    internal static LinuxDaemonHandshakeTransport.ProbeResult ProbeDaemonHandshakeTransportWithinBudget(string socketPath, TimeSpan timeout)
    {
        return LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, timeout);
    }

    internal static LinuxDaemonSocketAccessResult ProbeDaemonSocketAccess(
        LinuxDaemonSocketProbeOptions options,
        Func<string, LinuxDaemonSocketMetadata> getSocketMetadata,
        Func<string, LinuxDaemonGroupDefinition?> getGroupDefinition,
        Func<LinuxDaemonCurrentUserGroups> getCurrentUserGroups,
        Func<string, LinuxDaemonSocketAccessStatus> probeSocketAccess)
    {
        ArgumentNullException.ThrowIfNull(getSocketMetadata);
        ArgumentNullException.ThrowIfNull(getGroupDefinition);
        ArgumentNullException.ThrowIfNull(getCurrentUserGroups);
        ArgumentNullException.ThrowIfNull(probeSocketAccess);

        LinuxDaemonSocketMetadata metadata;
        try
        {
            metadata = getSocketMetadata(options.SocketPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new LinuxDaemonSocketAccessResult(
                options.SocketPath,
                LinuxDaemonSocketAccessStatus.PermissionDenied,
                Exception: ex,
                Message: ex.Message);
        }
        catch (Exception ex)
        {
            return new LinuxDaemonSocketAccessResult(
                options.SocketPath,
                LinuxDaemonSocketAccessStatus.UnexpectedError,
                Exception: ex,
                Message: ex.Message);
        }

        if (metadata.EntryKind == LinuxFileSystemEntryKind.Missing)
        {
            return LinuxDaemonSocketAccessResult.Missing(options.SocketPath);
        }

        if (metadata.EntryKind != LinuxFileSystemEntryKind.Socket)
        {
            return new LinuxDaemonSocketAccessResult(
                options.SocketPath,
                LinuxDaemonSocketAccessStatus.WrongType,
                Metadata: metadata);
        }

        var membership = ResolveDaemonGroupMembership(
            options.RequiredGroupName,
            getGroupDefinition,
            getCurrentUserGroups);

        LinuxDaemonSocketAccessStatus status;
        string? message = null;
        Exception? exception = null;
        try
        {
            status = probeSocketAccess(options.SocketPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            status = LinuxDaemonSocketAccessStatus.PermissionDenied;
            message = ex.Message;
            exception = ex;
        }
        catch (TimeoutException ex)
        {
            status = LinuxDaemonSocketAccessStatus.Timeout;
            message = ex.Message;
            exception = ex;
        }
        catch (Exception ex)
        {
            status = LinuxDaemonSocketAccessStatus.UnexpectedError;
            message = ex.Message;
            exception = ex;
        }

        return new LinuxDaemonSocketAccessResult(
            options.SocketPath,
            status,
            membership.Status,
            metadata,
            membership,
            message,
            exception);
    }

    internal static LinuxDaemonGroupMembershipResult ResolveDaemonGroupMembership(
        string groupName,
        Func<string, LinuxDaemonGroupDefinition?> getGroupDefinition,
        Func<LinuxDaemonCurrentUserGroups> getCurrentUserGroups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(getGroupDefinition);
        ArgumentNullException.ThrowIfNull(getCurrentUserGroups);

        try
        {
            var group = getGroupDefinition(groupName);
            if (group is null)
            {
                return new LinuxDaemonGroupMembershipResult(groupName, LinuxDaemonGroupMembershipStatus.MissingGroup);
            }

            var userGroups = getCurrentUserGroups();
            var isEffectiveMember = userGroups.PrimaryGroupId == group.GroupId ||
                                    userGroups.SupplementaryGroupIds.Contains(group.GroupId);
        if (isEffectiveMember)
        {
            return new LinuxDaemonGroupMembershipResult(
                groupName,
                LinuxDaemonGroupMembershipStatus.Member,
                group.GroupId,
                userGroups.UserName,
                userGroups.UserId,
                GetCurrentProcessGroupIds(userGroups));
        }

            var isConfiguredMember = group.MemberNames.Contains(userGroups.UserName, StringComparer.Ordinal);
            var status = isConfiguredMember
                ? LinuxDaemonGroupMembershipStatus.StaleSession
                : LinuxDaemonGroupMembershipStatus.UserNotMember;

            return new LinuxDaemonGroupMembershipResult(
                groupName,
                status,
                group.GroupId,
                userGroups.UserName,
                userGroups.UserId,
                GetCurrentProcessGroupIds(userGroups));
        }
        catch (Exception ex)
        {
            return new LinuxDaemonGroupMembershipResult(
                groupName,
                LinuxDaemonGroupMembershipStatus.Unknown,
                Message: ex.Message,
                Exception: ex);
        }
    }

    private static int[] GetCurrentProcessGroupIds(LinuxDaemonCurrentUserGroups userGroups)
    {
        return userGroups.SupplementaryGroupIds
            .Prepend(userGroups.PrimaryGroupId)
            .Distinct()
            .ToArray();
    }

    internal static LinuxDaemonHandshakeProbeResult ProbeStructuredDaemonHandshakeWithinBudget(string socketPath, TimeSpan timeout)
    {
        return MapDaemonHandshakeTransportResult(
            socketPath,
            timeout,
            ProbeDaemonHandshakeTransportWithinBudget(socketPath, timeout));
    }

    internal static LinuxDaemonHandshakeProbeResult MapDaemonHandshakeTransportResult(
        string socketPath,
        TimeSpan timeout,
        LinuxDaemonHandshakeTransport.ProbeResult result)
    {
        if (result.Succeeded)
        {
            return LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout);
        }

        var status = GetHandshakeStatus(result);
        return LinuxDaemonHandshakeProbeResult.Failed(
            socketPath,
            timeout,
            status,
            result.Failure?.Message,
            result.Failure);
    }

    internal static LinuxDirectInputFallbackResult GetDirectInputFallbackResult(
        bool canWriteUInput,
        bool canReadInputEvents)
    {
        return LinuxDirectInputFallbackResult.FromAccess(canWriteUInput, canReadInputEvents);
    }

    private static LinuxDaemonHandshakeStatus GetHandshakeStatus(LinuxDaemonHandshakeTransport.ProbeResult result)
    {
        if (result.TimedOut)
        {
            return LinuxDaemonHandshakeStatus.Timeout;
        }

        if (result.Failure is IpcClientException ipcClientException)
        {
            return ipcClientException.Reason switch
            {
                IpcClientFailureReason.SocketNotFound => LinuxDaemonHandshakeStatus.MissingSocket,
                IpcClientFailureReason.ConnectFailed => LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale,
                IpcClientFailureReason.PermissionDenied => LinuxDaemonHandshakeStatus.PermissionDenied,
                IpcClientFailureReason.HandshakeFailed => LinuxDaemonHandshakeStatus.HandshakeRejected,
                IpcClientFailureReason.ProtocolMismatch => LinuxDaemonHandshakeStatus.ProtocolMismatch,
                IpcClientFailureReason.Timeout => LinuxDaemonHandshakeStatus.Timeout,
                _ => LinuxDaemonHandshakeStatus.UnexpectedError
            };
        }

        return LinuxDaemonHandshakeTransport.MapFailure(result.Failure);
    }

    internal static bool CanOpenForWrite(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (File.OpenWrite(path))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    internal static bool CanOpenForRead(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string[] GetInputEventCandidates()
    {
        try
        {
            if (!Directory.Exists("/dev/input"))
            {
                return [];
            }

            return Directory.GetFiles("/dev/input", "event*");
        }
        catch
        {
            return [];
        }
    }
}
