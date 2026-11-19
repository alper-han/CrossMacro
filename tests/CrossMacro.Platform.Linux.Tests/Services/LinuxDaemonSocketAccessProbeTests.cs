
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

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

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

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

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

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.UserNotMember, result.GroupMembershipStatus);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task Probe_WhenMetadataDelegateThrowsUnexpectedError_ReturnsUnexpectedError()
    {
        var exception = new IOException("metadata unavailable");
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: _ => throw exception,
            getGroupDefinition: (_, _) => throw new InvalidOperationException("group lookup should not run"),
            getCurrentUserGroups: () => throw new InvalidOperationException("current user lookup should not run"),
            probeSocketAccess: (_, _) => throw new InvalidOperationException("socket access should not run"));

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonSocketAccessStatus.UnexpectedError, result.Status);
        Assert.Equal(exception.Message, result.Message);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task Probe_WhenGroupLookupIsCanceled_RethrowsCancellationAndPreservesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var expectedToken = cancellation.Token;
        await cancellation.CancelAsync();
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: (_, observedToken) =>
            {
                Assert.Equal(expectedToken, observedToken);
                return ValueTask.FromCanceled<LinuxDaemonGroupDefinition?>(observedToken);
            },
            getCurrentUserGroups: () => throw new InvalidOperationException("current user lookup should not run"),
            probeSocketAccess: (_, _) => throw new InvalidOperationException("socket access should not run"));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probe.ProbeAsync(DefaultOptions(), expectedToken).AsTask());
    }

    [Fact]
    public async Task Probe_WhenPathIsNotASocket_ReturnsWrongTypeWithoutGroupLookup()
    {
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: path => new LinuxDaemonSocketMetadata(path, LinuxFileSystemEntryKind.File),
            getGroupDefinition: (_, _) => throw new InvalidOperationException("group lookup should not run"),
            getCurrentUserGroups: () => throw new InvalidOperationException("current user lookup should not run"),
            probeSocketAccess: (_, _) => throw new InvalidOperationException("socket access should not run"));

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonSocketAccessStatus.WrongType, result.Status);
        Assert.Equal(LinuxFileSystemEntryKind.File, result.Metadata?.EntryKind);
    }

    [Fact]
    public async Task Probe_WhenSocketAccessibleAndUserIsEffectiveMember_ReturnsAccessibleMember()
    {
        var probe = CreateProbe(
            group: new LinuxDaemonGroupDefinition("crossmacro", 42, ["daemon"]),
            user: new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [42]),
            access: LinuxDaemonSocketAccessStatus.Accessible);

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.True(result.IsAccessible);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Member, result.GroupMembershipStatus);
        Assert.True(result.GroupMembership?.IsCurrentSessionMember);
    }

    [Fact]
    public async Task Probe_WhenUserIsNotConfiguredMember_ReturnsUserNotMember()
    {
        var probe = CreateProbe(
            group: new LinuxDaemonGroupDefinition("crossmacro", 42, ["daemon"]),
            user: new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [1000]),
            access: LinuxDaemonSocketAccessStatus.PermissionDenied);

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.UserNotMember, result.GroupMembershipStatus);
    }

    [Fact]
    public async Task Probe_WhenRequiredGroupDoesNotExist_ReturnsMissingGroup()
    {
        var probe = CreateProbe(
            group: null,
            user: new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [42]),
            access: LinuxDaemonSocketAccessStatus.PermissionDenied);

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonGroupMembershipStatus.MissingGroup, result.GroupMembershipStatus);
    }

    [Fact]
    public async Task Probe_WhenGroupLookupFails_ReportsUnknownMembershipAndKeepsAccessStatus()
    {
        var exception = new IOException("group database unavailable");
        var probe = new LinuxDaemonSocketAccessProbe(
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: (_, _) => throw exception,
            getCurrentUserGroups: () => new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [42]),
            probeSocketAccess: (_, _) => ValueTask.FromResult((LinuxDaemonSocketAccessStatus.Accessible, (string?)null, (Exception?)null)));

        var result = await probe.ProbeAsync(DefaultOptions(), CancellationToken.None);

        Assert.Equal(LinuxDaemonSocketAccessStatus.Accessible, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
        Assert.Same(exception, result.GroupMembership?.Exception);
    }

    private static LinuxDaemonSocketAccessProbe CreateProbe(
        LinuxDaemonGroupDefinition? group,
        LinuxDaemonCurrentUserGroups user,
        LinuxDaemonSocketAccessStatus access) =>
        new(
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: (_, _) => ValueTask.FromResult(group),
            getCurrentUserGroups: () => user,
            probeSocketAccess: (_, _) => ValueTask.FromResult((access, (string?)null, (Exception?)null)));

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
