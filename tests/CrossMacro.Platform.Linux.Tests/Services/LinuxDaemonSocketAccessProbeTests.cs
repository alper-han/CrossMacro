
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDaemonSocketAccessProbeTests
{
    [Fact]
    public async Task Probe_WhenSocketMissing_UsesInjectedMetadataAndSkipsGroupAndAccessDelegates()
    {
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: path => new LinuxDaemonSocketMetadata(path, LinuxFileSystemEntryKind.Missing),
            getGroupDefinition: (_, _) => throw new InvalidOperationException("group lookup should not run"),
            getCurrentUserGroups: () => throw new InvalidOperationException("current user lookup should not run"),
            probeSocketAccess: (_, _) => throw new InvalidOperationException("socket access should not run"));

        var result = await probe.ProbeAsync(DefaultOptions());

        Assert.Equal(LinuxDaemonSocketAccessStatus.Missing, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
    }

    [Fact]
    public async Task Probe_WhenConfiguredUserMissingCurrentGroup_ReturnsStaleSession()
    {
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: (_, _) => ValueTask.FromResult<LinuxDaemonGroupDefinition?>(new LinuxDaemonGroupDefinition("crossmacro", 42, ["alice"])),
            getCurrentUserGroups: () => new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [1000]),
            probeSocketAccess: (_, _) => ValueTask.FromResult((LinuxDaemonSocketAccessStatus.PermissionDenied, (string?)null, (Exception?)null)));

        var result = await probe.ProbeAsync(DefaultOptions());

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.StaleSession, result.GroupMembershipStatus);
        Assert.Equal("alice", result.GroupMembership?.UserName);
    }

    [Fact]
    public async Task Probe_WhenAccessDelegateThrowsUnauthorized_ReturnsPermissionDeniedWithMembership()
    {
        var exception = new UnauthorizedAccessException("denied");
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: (_, _) => ValueTask.FromResult<LinuxDaemonGroupDefinition?>(new LinuxDaemonGroupDefinition("crossmacro", 42, ["daemon"])),
            getCurrentUserGroups: () => new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [1000]),
            probeSocketAccess: (_, _) => ValueTask.FromException<(LinuxDaemonSocketAccessStatus Status, string? Message, Exception? Exception)>(exception));

        var result = await probe.ProbeAsync(DefaultOptions());

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.UserNotMember, result.GroupMembershipStatus);
        Assert.Same(exception, result.Exception);
    }

    private static LinuxDaemonSocketProbeOptions DefaultOptions()
    {
        return new LinuxDaemonSocketProbeOptions(IpcProtocol.DefaultSocketPath, "crossmacro");
    }

    private static LinuxDaemonSocketMetadata SocketMetadata(string path)
    {
        return new LinuxDaemonSocketMetadata(
            path,
            LinuxFileSystemEntryKind.Socket,
            OwnerUserId: 0,
            OwnerGroupId: 42,
            Permissions: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
    }
}
