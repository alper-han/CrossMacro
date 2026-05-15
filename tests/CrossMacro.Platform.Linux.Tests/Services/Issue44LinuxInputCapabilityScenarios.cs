using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.Tests.Services;

internal sealed class Issue44LinuxInputCapabilityScenario
{
    private readonly HashSet<string> _existingPaths = [];
    private readonly HashSet<string> _writablePaths = [];
    private readonly HashSet<string> _readablePaths = [];
    private readonly string[] _eventCandidates;

    private Issue44LinuxInputCapabilityScenario(string name, DateTime now, string[]? eventCandidates = null)
    {
        Name = name;
        Now = now;
        _eventCandidates = eventCandidates ?? [];
    }

    public string Name { get; }

    public DateTime Now { get; set; }

    public LinuxDaemonGroupDefinition? RequiredGroup { get; private set; }

    public LinuxDaemonCurrentUserGroups CurrentUserGroups { get; private init; } = new(1000, "alice", 1000, [1000]);

    public LinuxDaemonSocketMetadata SocketMetadata { get; private init; } = new(
        IpcProtocol.DefaultSocketPath,
        LinuxFileSystemEntryKind.Socket,
        OwnerUserId: 0,
        OwnerGroupId: 42,
        Permissions: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);

    public LinuxDaemonSocketAccessStatus SocketAccessStatus { get; private init; } = LinuxDaemonSocketAccessStatus.PermissionDenied;

    public LinuxInputCapabilityDetector.DaemonHandshakeProbeResult ProbeResult { get; private init; } = LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed();

    public LinuxDaemonHandshakeProbeResult StructuredHandshake { get; private init; } = LinuxDaemonHandshakeProbeResult.Failed(
        IpcProtocol.DefaultSocketPath,
        TimeSpan.FromSeconds(5),
        LinuxDaemonHandshakeStatus.PermissionDenied,
        exception: new UnauthorizedAccessException("socket access denied"));

    public static Issue44LinuxInputCapabilityScenario Issue44MissingCrossmacroGroup()
    {
        var scenario = WithSocketPermissionDenied(nameof(Issue44MissingCrossmacroGroup));
        scenario.RequiredGroup = null;

        return scenario;
    }

    public static Issue44LinuxInputCapabilityScenario Issue44StaleCrossmacroSession()
    {
        var scenario = WithSocketPermissionDenied(nameof(Issue44StaleCrossmacroSession));
        scenario.RequiredGroup = new LinuxDaemonGroupDefinition("crossmacro", 42, ["alice"]);

        return scenario;
    }

    public static Issue44LinuxInputCapabilityScenario SocketPermissionDenied()
    {
        return WithSocketPermissionDenied(nameof(SocketPermissionDenied));
    }

    public static Issue44LinuxInputCapabilityScenario DirectFallbackAvailable()
    {
        var scenario = new Issue44LinuxInputCapabilityScenario(nameof(DirectFallbackAvailable), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["/dev/input/event0"])
        {
            RequiredGroup = new LinuxDaemonGroupDefinition("crossmacro", 42, ["alice"]),
            CurrentUserGroups = new LinuxDaemonCurrentUserGroups(1000, "alice", 1000, [42]),
            SocketAccessStatus = LinuxDaemonSocketAccessStatus.Missing,
            StructuredHandshake = LinuxDaemonHandshakeProbeResult.Failed(
                IpcProtocol.DefaultSocketPath,
                TimeSpan.FromSeconds(5),
                LinuxDaemonHandshakeStatus.MissingSocket),
            ProbeResult = LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed()
        };

        scenario._existingPaths.Add(LinuxConstants.UInputAlternatePath);
        scenario._writablePaths.Add(LinuxConstants.UInputAlternatePath);
        scenario._readablePaths.Add("/dev/input/event0");
        return scenario;
    }

    public LinuxInputCapabilityDetector CreateDetector()
    {
        return new LinuxInputCapabilityDetector(
            FileExists,
            CanOpenForWrite,
            CanOpenForRead,
            ProbeDaemonHandshake,
            GetInputEventCandidates,
            () => Now);
    }

    public LinuxDaemonSocketAccessResult CreateSocketAccessResult()
    {
        var membership = ResolveGroupMembership();
        return new LinuxDaemonSocketAccessResult(
            IpcProtocol.DefaultSocketPath,
            SocketAccessStatus,
            membership.Status,
            SocketMetadata,
            membership);
    }

    public LinuxDaemonDiagnosticSnapshot CreateDiagnosticSnapshot()
    {
        return new LinuxDaemonDiagnosticSnapshot(
            IpcProtocol.DefaultSocketPath,
            CreateSocketAccessResult(),
            ResolveGroupMembership().Status,
            LinuxDirectInputFallbackResult.FromAccess(CanOpenForWrite(LinuxConstants.UInputAlternatePath), CanOpenForRead("/dev/input/event0")),
            StructuredHandshake);
    }

    public bool FileExists(string path)
    {
        return _existingPaths.Contains(path);
    }

    public bool CanOpenForWrite(string path)
    {
        return _writablePaths.Contains(path);
    }

    public bool CanOpenForRead(string path)
    {
        return _readablePaths.Contains(path);
    }

    public string[] GetInputEventCandidates()
    {
        return _eventCandidates;
    }

    public LinuxInputCapabilityDetector.DaemonHandshakeProbeResult ProbeDaemonHandshake(string socketPath, TimeSpan timeout)
    {
        return socketPath == IpcProtocol.DefaultSocketPath ? ProbeResult : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed();
    }

    private static Issue44LinuxInputCapabilityScenario WithSocketPermissionDenied(string name)
    {
        var scenario = new Issue44LinuxInputCapabilityScenario(name, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        {
            RequiredGroup = new LinuxDaemonGroupDefinition("crossmacro", 42, ["daemon"]),
            SocketAccessStatus = LinuxDaemonSocketAccessStatus.PermissionDenied,
            ProbeResult = LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(new UnauthorizedAccessException("socket access denied")),
            StructuredHandshake = LinuxDaemonHandshakeProbeResult.Failed(
                IpcProtocol.DefaultSocketPath,
                TimeSpan.FromSeconds(5),
                LinuxDaemonHandshakeStatus.PermissionDenied,
                exception: new UnauthorizedAccessException("socket access denied"))
        };

        scenario._existingPaths.Add(IpcProtocol.DefaultSocketPath);
        return scenario;
    }

    private LinuxDaemonGroupMembershipResult ResolveGroupMembership()
    {
        if (RequiredGroup is null)
        {
            return new LinuxDaemonGroupMembershipResult(
                "crossmacro",
                LinuxDaemonGroupMembershipStatus.MissingGroup,
                UserName: CurrentUserGroups.UserName,
                UserId: CurrentUserGroups.UserId,
                CurrentProcessGroupIds: CurrentProcessGroupIds());
        }

        if (CurrentUserGroups.SupplementaryGroupIds.Contains(RequiredGroup.GroupId) || CurrentUserGroups.PrimaryGroupId == RequiredGroup.GroupId)
        {
            return new LinuxDaemonGroupMembershipResult(
                RequiredGroup.Name,
                LinuxDaemonGroupMembershipStatus.Member,
                RequiredGroup.GroupId,
                CurrentUserGroups.UserName,
                CurrentUserGroups.UserId,
                CurrentProcessGroupIds());
        }

        var status = RequiredGroup.MemberNames.Contains(CurrentUserGroups.UserName, StringComparer.Ordinal)
            ? LinuxDaemonGroupMembershipStatus.StaleSession
            : LinuxDaemonGroupMembershipStatus.UserNotMember;

        return new LinuxDaemonGroupMembershipResult(
            RequiredGroup.Name,
            status,
            RequiredGroup.GroupId,
            CurrentUserGroups.UserName,
            CurrentUserGroups.UserId,
            CurrentProcessGroupIds());
    }

    private int[] CurrentProcessGroupIds()
    {
        return CurrentUserGroups.SupplementaryGroupIds
            .Prepend(CurrentUserGroups.PrimaryGroupId)
            .Distinct()
            .ToArray();
    }
}

internal static class Issue44PackagingTextAssertions
{
    public static void AssertMentionsCrossmacroGroupRemediation(string text)
    {
        Assert.Contains("crossmacro", text, StringComparison.Ordinal);
        Assert.Contains("group", text, StringComparison.OrdinalIgnoreCase);
    }
}
