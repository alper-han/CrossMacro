using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using CrossMacro.Infrastructure.Linux.Native;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxDaemonSocketAccessProbe : ILinuxDaemonSocketAccessProbe
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);

    private readonly Func<string, LinuxDaemonSocketMetadata> _getSocketMetadata;
    private readonly Func<string, LinuxDaemonGroupDefinition?> _getGroupDefinition;
    private readonly Func<LinuxDaemonCurrentUserGroups> _getCurrentUserGroups;
    private readonly Func<string, LinuxDaemonSocketAccessStatus> _probeSocketAccess;

    public LinuxDaemonSocketAccessProbe()
        : this(GetSocketMetadata, GetGroupDefinition, GetCurrentUserGroups, ProbeSocketAccess)
    {
    }

    internal LinuxDaemonSocketAccessProbe(
        Func<string, LinuxDaemonSocketMetadata> getSocketMetadata,
        Func<string, LinuxDaemonGroupDefinition?> getGroupDefinition,
        Func<LinuxDaemonCurrentUserGroups> getCurrentUserGroups,
        Func<string, LinuxDaemonSocketAccessStatus> probeSocketAccess)
    {
        _getSocketMetadata = getSocketMetadata ?? throw new ArgumentNullException(nameof(getSocketMetadata));
        _getGroupDefinition = getGroupDefinition ?? throw new ArgumentNullException(nameof(getGroupDefinition));
        _getCurrentUserGroups = getCurrentUserGroups ?? throw new ArgumentNullException(nameof(getCurrentUserGroups));
        _probeSocketAccess = probeSocketAccess ?? throw new ArgumentNullException(nameof(probeSocketAccess));
    }

    public LinuxDaemonSocketAccessResult Probe(LinuxDaemonSocketProbeOptions options)
    {
        return LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            options,
            _getSocketMetadata,
            _getGroupDefinition,
            _getCurrentUserGroups,
            _probeSocketAccess);
    }

    private static LinuxDaemonSocketMetadata GetSocketMetadata(string path)
    {
        if (lstat(path, out var stat) != 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            if (errno == ErrNo.ENOENT || errno == ErrNo.ENOTDIR)
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

    private static LinuxDaemonGroupDefinition? GetGroupDefinition(string groupName)
    {
        foreach (var line in File.ReadLines(LinuxSystemPaths.GroupFile))
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
                .Where(member => member.Length > 0)
                .ToArray();

            return new LinuxDaemonGroupDefinition(parts[0], gid, members);
        }

        return null;
    }

    private static LinuxDaemonCurrentUserGroups GetCurrentUserGroups()
    {
        var groupCount = getgroups(0, null);
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

    private static LinuxDaemonSocketAccessStatus ProbeSocketAccess(string socketPath)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeout = new CancellationTokenSource(ConnectTimeout);

        try
        {
            socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token).GetAwaiter().GetResult();
            return LinuxDaemonSocketAccessStatus.Accessible;
        }
        catch (OperationCanceledException)
        {
            return LinuxDaemonSocketAccessStatus.Timeout;
        }
        catch (UnauthorizedAccessException)
        {
            return LinuxDaemonSocketAccessStatus.PermissionDenied;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
        {
            return LinuxDaemonSocketAccessStatus.PermissionDenied;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            return LinuxDaemonSocketAccessStatus.Timeout;
        }
        catch (SocketException)
        {
            return LinuxDaemonSocketAccessStatus.UnexpectedError;
        }
    }

    private static LinuxFileSystemEntryKind GetEntryKind(uint mode)
    {
        return (mode & FileTypeMask) switch
        {
            SocketFileType => LinuxFileSystemEntryKind.Socket,
            DirectoryFileType => LinuxFileSystemEntryKind.Directory,
            RegularFileType => LinuxFileSystemEntryKind.File,
            _ => LinuxFileSystemEntryKind.Other
        };
    }

    private static void ThrowForErrno(int errno, string target)
    {
        var exception = new IOException($"Linux diagnostic probe failed for {target}: errno {errno}.");
        if (errno == ErrNo.EACCES || errno == ErrNo.EPERM)
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

    [DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
    private static extern int lstat(string path, out LinuxStat stat);

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    [DllImport("libc", SetLastError = true)]
    private static extern uint getegid();

    [DllImport("libc", SetLastError = true)]
    private static extern int getgroups(int size, [Out] int[]? groups);

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
