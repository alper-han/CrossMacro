using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputProbeUtilitiesTests
{
    [LinuxFact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketExists_ReturnsDefaultSocketPath()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(path => path == IpcProtocol.DefaultSocketPath);

        Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
    }

    [LinuxFact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketMissing_ReturnsNull()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(_ => false);

        Assert.Null(socketPath);
    }

    [LinuxFact]
    public void HasUInputWriteAccess_WhenAlternatePathWritable_ReturnsTrue()
    {
        var result = LinuxInputProbeUtilities.HasUInputWriteAccess(path => path == LinuxConstants.UInputAlternatePath);

        Assert.True(result);
    }

    [LinuxFact]
    public void HasReadableInputEventAccess_WhenReadableCandidateExists_ReturnsTrue()
    {
        var result = LinuxInputProbeUtilities.HasReadableInputEventAccess(
            canOpenForRead: path => path == "/dev/input/event7",
            getInputEventCandidates: () => ["/dev/input/event5", "/dev/input/event7"]);

        Assert.True(result);
    }

    [LinuxFact]
    public void HasReadableInputEventAccess_WhenNoReadableCandidateExists_ReturnsFalse()
    {
        var result = LinuxInputProbeUtilities.HasReadableInputEventAccess(
            canOpenForRead: _ => false,
            getInputEventCandidates: () => ["/dev/input/event5", "/dev/input/event7"]);

        Assert.False(result);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenSocketMissing_ReturnsMissingWithUnknownGroup()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: path => new LinuxDaemonSocketMetadata(path, LinuxFileSystemEntryKind.Missing),
            getGroupDefinition: _ => throw new InvalidOperationException("group lookup should not run"),
            getCurrentUserGroups: () => throw new InvalidOperationException("user lookup should not run"),
            probeSocketAccess: _ => throw new InvalidOperationException("access probe should not run"));

        Assert.Equal(LinuxDaemonSocketAccessStatus.Missing, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
        Assert.Equal(IpcProtocol.DefaultSocketPath, result.SocketPath);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenMetadataPermissionDenied_ReturnsPermissionDenied()
    {
        var exception = new UnauthorizedAccessException("parent denied");

        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: _ => throw exception,
            getGroupDefinition: _ => CrossmacroGroup(),
            getCurrentUserGroups: () => MemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.Accessible);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
        Assert.Same(exception, result.Exception);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenPathIsWrongType_ReturnsWrongType()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: path => new LinuxDaemonSocketMetadata(path, LinuxFileSystemEntryKind.File),
            getGroupDefinition: _ => CrossmacroGroup(),
            getCurrentUserGroups: () => MemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.Accessible);

        Assert.Equal(LinuxDaemonSocketAccessStatus.WrongType, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
        Assert.Equal(LinuxFileSystemEntryKind.File, result.Metadata?.EntryKind);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenSocketAccessibleAndUserMember_ReturnsAccessibleMember()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: _ => CrossmacroGroup(),
            getCurrentUserGroups: () => MemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.Accessible);

        Assert.Equal(LinuxDaemonSocketAccessStatus.Accessible, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Member, result.GroupMembershipStatus);
        Assert.True(result.IsAccessible);
        Assert.True(result.GroupMembership?.IsCurrentSessionMember);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenUserNotConfiguredMember_ReturnsUserNotMember()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: _ => CrossmacroGroup(),
            getCurrentUserGroups: () => NonMemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.PermissionDenied);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.UserNotMember, result.GroupMembershipStatus);
        Assert.Equal("alice", result.GroupMembership?.UserName);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenConfiguredMemberMissingEffectiveGroup_ReturnsStaleSession()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: _ => CrossmacroGroup(["alice"]),
            getCurrentUserGroups: () => NonMemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.PermissionDenied);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.StaleSession, result.GroupMembershipStatus);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenRequiredGroupMissing_ReturnsMissingGroup()
    {
        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: _ => null,
            getCurrentUserGroups: () => MemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.PermissionDenied);

        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.MissingGroup, result.GroupMembershipStatus);
    }

    [LinuxFact]
    public void ProbeDaemonSocketAccess_WhenGroupLookupFails_ReturnsUnknownGroup()
    {
        var exception = new IOException("group database unavailable");

        var result = LinuxInputProbeUtilities.ProbeDaemonSocketAccess(
            DefaultProbeOptions(),
            getSocketMetadata: SocketMetadata,
            getGroupDefinition: _ => throw exception,
            getCurrentUserGroups: () => MemberUserGroups(),
            probeSocketAccess: _ => LinuxDaemonSocketAccessStatus.Accessible);

        Assert.Equal(LinuxDaemonSocketAccessStatus.Accessible, result.Status);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Unknown, result.GroupMembershipStatus);
        Assert.Same(exception, result.GroupMembership?.Exception);
    }

    [LinuxFact]
    public void MapDaemonHandshakeTransportResult_WhenTransportSucceeds_ReturnsStructuredSuccess()
    {
        var timeout = TimeSpan.FromSeconds(1);

        var result = LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            IpcProtocol.DefaultSocketPath,
            timeout,
            LinuxDaemonHandshakeTransport.ProbeResult.Success());

        Assert.True(result.Succeeded);
        Assert.False(result.TimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.Success, result.Status);
        Assert.Equal(IpcProtocol.DefaultSocketPath, result.SocketPath);
        Assert.Equal(timeout, result.Timeout);
    }

    [LinuxTheory]
    [InlineData(IpcClientFailureReason.SocketNotFound, LinuxDaemonHandshakeStatus.MissingSocket)]
    [InlineData(IpcClientFailureReason.ConnectFailed, LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale)]
    [InlineData(IpcClientFailureReason.ProtocolMismatch, LinuxDaemonHandshakeStatus.ProtocolMismatch)]
    [InlineData(IpcClientFailureReason.HandshakeFailed, LinuxDaemonHandshakeStatus.HandshakeRejected)]
    [InlineData(IpcClientFailureReason.Timeout, LinuxDaemonHandshakeStatus.Timeout)]
    public void MapDaemonHandshakeTransportResult_WhenIpcClientFails_ReturnsStructuredStatus(
        IpcClientFailureReason failureReason,
        LinuxDaemonHandshakeStatus expectedStatus)
    {
        var exception = new IpcClientException(failureReason, "probe failed");

        var result = LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(1),
            LinuxDaemonHandshakeTransport.ProbeResult.Failed(exception));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [LinuxFact]
    public void MapDaemonHandshakeTransportResult_WhenTransportTimesOut_ReturnsStructuredTimeout()
    {
        var exception = new TimeoutException("probe timed out");

        var result = LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(1),
            LinuxDaemonHandshakeTransport.ProbeResult.Timeout(exception));

        Assert.True(result.TimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [LinuxFact]
    public void MapDaemonHandshakeTransportResult_WhenAccessDenied_ReturnsPermissionDenied()
    {
        var exception = new UnauthorizedAccessException("socket access denied");

        var result = LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(1),
            LinuxDaemonHandshakeTransport.ProbeResult.Failed(exception));

        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [LinuxFact]
    public void MapDaemonHandshakeTransportResult_WhenWrongTypeIoFailure_ReturnsWrongSocketType()
    {
        var exception = new IOException("Endpoint is not a socket.");

        var result = LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(1),
            LinuxDaemonHandshakeTransport.ProbeResult.Failed(exception));

        Assert.Equal(LinuxDaemonHandshakeStatus.WrongSocketType, result.Status);
        Assert.Same(exception, result.Exception);
    }

    [LinuxFact]
    public void LinuxDaemonDiagnosticSnapshot_WhenSocketAndHandshakeSucceed_CanUseDaemon()
    {
        var socketAccess = LinuxDaemonSocketAccessResult.Accessible(
            IpcProtocol.DefaultSocketPath,
            LinuxDaemonGroupMembershipStatus.Member);
        var fallback = LinuxDirectInputFallbackResult.FromAccess(canWriteUInput: false, canReadInputEvents: false);
        var handshake = LinuxDaemonHandshakeProbeResult.Success(IpcProtocol.DefaultSocketPath, TimeSpan.FromSeconds(1));

        var snapshot = new LinuxDaemonDiagnosticSnapshot(
            IpcProtocol.DefaultSocketPath,
            socketAccess,
            LinuxDaemonGroupMembershipStatus.Member,
            fallback,
            handshake);

        Assert.True(snapshot.CanUseDaemon);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Member, snapshot.GroupMembershipStatus);
        Assert.Equal(LinuxDirectInputFallbackStatus.MissingUInputWriteAccess, snapshot.DirectInputFallback.Status);
    }

    private static LinuxDaemonSocketProbeOptions DefaultProbeOptions()
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

    private static LinuxDaemonGroupDefinition CrossmacroGroup(string[]? memberNames = null)
    {
        return new LinuxDaemonGroupDefinition("crossmacro", 42, memberNames ?? ["daemon"]);
    }

    private static LinuxDaemonCurrentUserGroups MemberUserGroups()
    {
        return new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [42]);
    }

    private static LinuxDaemonCurrentUserGroups NonMemberUserGroups()
    {
        return new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [1000]);
    }
}
