
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

    internal static ValueTask<string?> ResolveAvailableSocketPathAsync(
        Func<string, bool> fileExists,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ResolveAvailableSocketPath(fileExists));
    }

    internal static bool HasUInputWriteAccess(Func<string, bool> canOpenForWrite)
    {
        ArgumentNullException.ThrowIfNull(canOpenForWrite);

        return canOpenForWrite(LinuxConstants.UInputDevicePath) ||
               canOpenForWrite(LinuxConstants.UInputAlternatePath);
    }

    internal static ValueTask<bool> HasUInputWriteAccessAsync(
        Func<string, bool> canOpenForWrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canOpenForWrite);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            canOpenForWrite(LinuxConstants.UInputDevicePath) ||
            canOpenForWrite(LinuxConstants.UInputAlternatePath));
    }

    internal static bool HasReadableInputEventAccess(
        Func<string, bool> canOpenForRead,
        Func<string[]> getInputEventCandidates)
    {
        ArgumentNullException.ThrowIfNull(canOpenForRead);
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);

        var eventDevices = getInputEventCandidates();
        if (eventDevices.Length is 0)
        {
            return false;
        }

        return eventDevices.Any(canOpenForRead);
    }

    internal static async ValueTask<bool> HasReadableInputEventAccessAsync(
        Func<string, bool> canOpenForRead,
        Func<string[]> getInputEventCandidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canOpenForRead);
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);
        cancellationToken.ThrowIfCancellationRequested();

        var eventDevices = getInputEventCandidates();
        if (eventDevices.Length is 0)
        {
            return false;
        }

        foreach (var eventDevice in eventDevices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await CanOpenForReadAsync(eventDevice, canOpenForRead, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    internal static LinuxDaemonHandshakeTransport.ProbeResult ProbeDaemonHandshakeTransportWithinBudget(string socketPath, TimeSpan timeout)
    {
        return LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, timeout);
    }

    internal static ValueTask<LinuxDaemonHandshakeTransport.ProbeResult> ProbeDaemonHandshakeTransportWithinBudgetAsync(
        string socketPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return new(LinuxDaemonHandshakeTransport.ProbeWithinBudgetAsync(socketPath, timeout, cancellationToken));
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

        if (TryGetSocketMetadataOrError(options.SocketPath, getSocketMetadata, out var metadata, out var errorResult))
        {
            return errorResult;
        }

        if (metadata.EntryKind is LinuxFileSystemEntryKind.Missing)
        {
            return LinuxDaemonSocketAccessResult.Missing(options.SocketPath);
        }

        if (metadata.EntryKind is not LinuxFileSystemEntryKind.Socket)
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

        var (status, message, exception) = ProbeSocketStatus(options.SocketPath, probeSocketAccess);

        return new LinuxDaemonSocketAccessResult(
            options.SocketPath,
            status,
            membership.Status,
            metadata,
            membership,
            message,
            exception);
    }

    private static bool TryGetSocketMetadataOrError(
        string socketPath,
        Func<string, LinuxDaemonSocketMetadata> getSocketMetadata,
        out LinuxDaemonSocketMetadata metadata,
        out LinuxDaemonSocketAccessResult errorResult)
    {
        try
        {
            metadata = getSocketMetadata(socketPath);
            errorResult = default;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            metadata = default;
            errorResult = new LinuxDaemonSocketAccessResult(
                socketPath,
                LinuxDaemonSocketAccessStatus.PermissionDenied,
                Exception: ex,
                Message: ex.Message);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            metadata = default;
            errorResult = new LinuxDaemonSocketAccessResult(
                socketPath,
                LinuxDaemonSocketAccessStatus.UnexpectedError,
                Exception: ex,
                Message: ex.Message);
            return true;
        }
    }

    private static (LinuxDaemonSocketAccessStatus Status, string? Message, Exception? Exception) ProbeSocketStatus(
        string socketPath,
        Func<string, LinuxDaemonSocketAccessStatus> probeSocketAccess)
    {
        try
        {
            return (probeSocketAccess(socketPath), null, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (LinuxDaemonSocketAccessStatus.PermissionDenied, ex.Message, ex);
        }
        catch (TimeoutException ex)
        {
            return (LinuxDaemonSocketAccessStatus.Timeout, ex.Message, ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (LinuxDaemonSocketAccessStatus.UnexpectedError, ex.Message, ex);
        }
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
                _ => LinuxDaemonHandshakeStatus.UnexpectedError,
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static ValueTask<bool> CanOpenForReadAsync(
        string path,
        Func<string, bool> canOpenForRead,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(canOpenForRead(path));
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [];
        }
    }
}
