
namespace CrossMacro.Platform.Linux.Services;

internal sealed partial class LinuxDaemonSocketAccessProbe : ILinuxDaemonSocketAccessProbe
{
    private readonly Func<string, LinuxDaemonSocketMetadata> _getSocketMetadata;
    private readonly Func<string, CancellationToken, ValueTask<LinuxDaemonGroupDefinition?>> _getGroupDefinition;
    private readonly Func<LinuxDaemonCurrentUserGroups> _getCurrentUserGroups;
    private readonly Func<string, CancellationToken, ValueTask<(LinuxDaemonSocketAccessStatus Status, string? Message, Exception? Exception)>> _probeSocketAccess;

    public LinuxDaemonSocketAccessProbe()
        : this(GetSocketMetadata, GetGroupDefinitionAsync, GetCurrentUserGroups, ProbeSocketStatusAsync) { /* Empty */ }

    internal LinuxDaemonSocketAccessProbe(
        Func<string, LinuxDaemonSocketMetadata> getSocketMetadata,
        Func<string, CancellationToken, ValueTask<LinuxDaemonGroupDefinition?>> getGroupDefinition,
        Func<LinuxDaemonCurrentUserGroups> getCurrentUserGroups,
        Func<string, CancellationToken, ValueTask<(LinuxDaemonSocketAccessStatus Status, string? Message, Exception? Exception)>> probeSocketAccess)
    {
        _getSocketMetadata = getSocketMetadata ?? throw new ArgumentNullException(nameof(getSocketMetadata));
        _getGroupDefinition = getGroupDefinition ?? throw new ArgumentNullException(nameof(getGroupDefinition));
        _getCurrentUserGroups = getCurrentUserGroups ?? throw new ArgumentNullException(nameof(getCurrentUserGroups));
        _probeSocketAccess = probeSocketAccess ?? throw new ArgumentNullException(nameof(probeSocketAccess));
    }

    public async ValueTask<LinuxDaemonSocketAccessResult> ProbeAsync(LinuxDaemonSocketProbeOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = _getSocketMetadata(options.SocketPath);
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

            var membership = await ResolveDaemonGroupMembershipAsync(options.RequiredGroupName, cancellationToken).ConfigureAwait(false);

            LinuxDaemonSocketAccessStatus status;
            string? message;
            Exception? exception;
            try
            {
                (status, message, exception) = await _probeSocketAccess(options.SocketPath, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                status = LinuxDaemonSocketAccessStatus.PermissionDenied;
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            return new LinuxDaemonSocketAccessResult(
                options.SocketPath,
                LinuxDaemonSocketAccessStatus.PermissionDenied,
                Exception: ex,
                Message: ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new LinuxDaemonSocketAccessResult(
                options.SocketPath,
                LinuxDaemonSocketAccessStatus.UnexpectedError,
                Exception: ex,
                Message: ex.Message);
        }
    }

    private static LinuxDaemonSocketMetadata GetSocketMetadata(string path)
    {
        if (lstat(path, out var stat) is not 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            if (errno is ErrNo.ENOENT or ErrNo.ENOTDIR)
            {
                return new LinuxDaemonSocketMetadata(path, LinuxFileSystemEntryKind.Missing);
            }

            ThrowForErrno(errno, path);
        }

        return new LinuxDaemonSocketMetadata(
            path,
            GetEntryKind(stat.Mode),
            OwnerUserId: checked((int)stat.UserId),
            OwnerGroupId: checked((int)stat.GroupId),
            Permissions: (UnixFileMode)(stat.Mode & FilePermissionMask));
    }

    private static async ValueTask<LinuxDaemonGroupDefinition?> GetGroupDefinitionAsync(string groupName, CancellationToken cancellationToken)
    {
        await foreach (var line in File.ReadLinesAsync(LinuxSystemPaths.GroupFile, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
            {
                continue;
            }

            var parts = line.Split(':');
            if (parts.Length < 4 || !string.Equals(parts[0], groupName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gid))
            {
                return null;
            }

            var members = parts[3]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static member => member.Length > 0)
                .ToArray();

            return new LinuxDaemonGroupDefinition(parts[0], gid, members);
        }

        return null;
    }

    private static LinuxDaemonCurrentUserGroups GetCurrentUserGroups()
    {
        var groupCount = getgroups(0, groups: null);
        if (groupCount < 0)
        {
            ThrowForErrno(Marshal.GetLastPInvokeError(), "getgroups");
        }

        var supplementaryGroups = new int[groupCount];
        if (groupCount > 0 && getgroups(groupCount, supplementaryGroups) < 0)
        {
            ThrowForErrno(Marshal.GetLastPInvokeError(), "getgroups");
        }

        return new LinuxDaemonCurrentUserGroups(
            checked((int)geteuid()),
            Environment.UserName,
            checked((int)getegid()),
            supplementaryGroups);
    }

    private static async ValueTask<(LinuxDaemonSocketAccessStatus Status, string? Message, Exception? Exception)> ProbeSocketStatusAsync(string socketPath, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(1));

        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeoutSource.Token).ConfigureAwait(false);
            return (LinuxDaemonSocketAccessStatus.Accessible, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (LinuxDaemonSocketAccessStatus.Timeout, "Timed out while probing daemon socket access.", null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (LinuxDaemonSocketAccessStatus.PermissionDenied, ex.Message, ex);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AccessDenied)
        {
            return (LinuxDaemonSocketAccessStatus.PermissionDenied, ex.Message, ex);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionRefused)
        {
            return (LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale, ex.Message, ex);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.TimedOut)
        {
            return (LinuxDaemonSocketAccessStatus.Timeout, ex.Message, ex);
        }
        catch (SocketException ex)
        {
            return (LinuxDaemonSocketAccessStatus.UnexpectedError, ex.Message, ex);
        }
    }

    private async ValueTask<LinuxDaemonGroupMembershipResult> ResolveDaemonGroupMembershipAsync(string groupName, CancellationToken cancellationToken)
    {
        try
        {
            var group = await _getGroupDefinition(groupName, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                return new LinuxDaemonGroupMembershipResult(groupName, LinuxDaemonGroupMembershipStatus.MissingGroup);
            }

            var userGroups = _getCurrentUserGroups();
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
        catch (OperationCanceledException)
        {
            throw;
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

    private static LinuxFileSystemEntryKind GetEntryKind(uint mode)
    {
        return (mode & FileTypeMask) switch
        {
            SocketFileType => LinuxFileSystemEntryKind.Socket,
            DirectoryFileType => LinuxFileSystemEntryKind.Directory,
            RegularFileType => LinuxFileSystemEntryKind.File,
            _ => LinuxFileSystemEntryKind.Other,
        };
    }

    private static void ThrowForErrno(int errno, string target)
    {
        var exception = new IOException($"Linux diagnostic probe failed for {target}: errno {errno.ToString(CultureInfo.InvariantCulture)}.");
        if (errno is ErrNo.EACCES or ErrNo.EPERM)
        {
            throw new UnauthorizedAccessException(exception.Message, exception);
        }

        throw exception;
    }

    private const uint FileTypeMask = 0xF000;
    private const uint SocketFileType = 0xC000;
    private const uint DirectoryFileType = 0x4000;
    private const uint RegularFileType = 0x8000;
    private const uint FilePermissionMask = 0x0FFF;

    [LibraryImport("libc", SetLastError = true, EntryPoint = "lstat", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int lstat(string path, out LinuxStat stat);

    [LibraryImport("libc", SetLastError = true)]
    private static partial uint geteuid();

    [LibraryImport("libc", SetLastError = true)]
    private static partial uint getegid();

    [LibraryImport("libc", SetLastError = true)]
    private static partial int getgroups(int size, [Out] int[]? groups);

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxStat
    {
        public ulong Device;
        public ulong Inode;
        public ulong HardLinkCount;
        public uint Mode;
        public uint UserId;
        public uint GroupId;
        public int Padding0;
        public ulong RDevice;
        public long Size;
        public long BlockSize;
        public long BlockCount;
        public long AccessTimeSeconds;
        public long AccessTimeNanoseconds;
        public long ModifyTimeSeconds;
        public long ModifyTimeNanoseconds;
        public long ChangeTimeSeconds;
        public long ChangeTimeNanoseconds;
        public long Unused0;
        public long Unused1;
        public long Unused2;
    }

    private static class ErrNo
    {
        public const int EPERM = 1;
        public const int ENOENT = 2;
        public const int EACCES = 13;
        public const int ENOTDIR = 20;
    }
}
