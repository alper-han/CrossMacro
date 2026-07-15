using CrossMacro.Cli.Services;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Cli.Tests;

internal sealed class LinuxDoctorInputScenario
{
    private readonly HashSet<string> _existingPaths = [];
    private readonly HashSet<string> _writablePaths = [];
    private LinuxDaemonSocketAccessStatus _socketAccessStatus = LinuxDaemonSocketAccessStatus.Missing;
    private LinuxDaemonGroupMembershipStatus _groupMembershipStatus = LinuxDaemonGroupMembershipStatus.Unknown;
    private LinuxDaemonHandshakeStatus _handshakeStatus = LinuxDaemonHandshakeStatus.MissingSocket;

    private LinuxDoctorInputScenario(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string SessionType { get; private init; } = "wayland";

    public bool DaemonHandshakeSucceeds { get; private init; }

    public bool SocketProbePermissionDenied { get; private init; }

    public DoctorCheckStatus ExpectedHandshakeStatus { get; private init; }

    public DoctorCheckStatus ExpectedReadinessStatus { get; private init; }

    public static LinuxDoctorInputScenario SocketPermissionDenied(bool directFallbackAvailable = false)
    {
        var scenario = new LinuxDoctorInputScenario(nameof(SocketPermissionDenied))
        {
            SocketProbePermissionDenied = true,
            ExpectedHandshakeStatus = directFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            ExpectedReadinessStatus = directFallbackAvailable ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            _socketAccessStatus = LinuxDaemonSocketAccessStatus.PermissionDenied,
            _groupMembershipStatus = LinuxDaemonGroupMembershipStatus.UserNotMember,
            _handshakeStatus = LinuxDaemonHandshakeStatus.PermissionDenied,
        };

        scenario._existingPaths.Add(IpcProtocol.DefaultSocketPath);
        if (directFallbackAvailable)
        {
            scenario._existingPaths.Add("/dev/uinput");
            scenario._existingPaths.Add("/dev/input/event0");
            scenario._writablePaths.Add("/dev/uinput");
        }

        return scenario;
    }

    public static LinuxDoctorInputScenario DirectFallbackAvailable()
    {
        var scenario = new LinuxDoctorInputScenario(nameof(DirectFallbackAvailable))
        {
            ExpectedHandshakeStatus = DoctorCheckStatus.Warn,
            ExpectedReadinessStatus = DoctorCheckStatus.Pass,
            _socketAccessStatus = LinuxDaemonSocketAccessStatus.Missing,
            _handshakeStatus = LinuxDaemonHandshakeStatus.MissingSocket,
        };

        scenario._existingPaths.Add("/dev/uinput");
        scenario._existingPaths.Add("/dev/input/event0");
        scenario._writablePaths.Add("/dev/uinput");
        return scenario;
    }

    public static LinuxDoctorInputScenario SocketAccessible(
        LinuxDaemonGroupMembershipStatus groupMembershipStatus,
        LinuxDaemonHandshakeStatus handshakeStatus,
        bool directFallbackAvailable)
    {
        var scenario = new LinuxDoctorInputScenario(nameof(SocketAccessible))
        {
            DaemonHandshakeSucceeds = handshakeStatus is LinuxDaemonHandshakeStatus.Success,
            ExpectedHandshakeStatus = handshakeStatus is LinuxDaemonHandshakeStatus.Success
                ? DoctorCheckStatus.Pass
                : directFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            ExpectedReadinessStatus = handshakeStatus is LinuxDaemonHandshakeStatus.Success || directFallbackAvailable
                ? DoctorCheckStatus.Pass
                : DoctorCheckStatus.Fail,
            _socketAccessStatus = LinuxDaemonSocketAccessStatus.Accessible,
            _groupMembershipStatus = groupMembershipStatus,
            _handshakeStatus = handshakeStatus,
        };

        scenario._existingPaths.Add(IpcProtocol.DefaultSocketPath);
        if (directFallbackAvailable)
        {
            scenario._existingPaths.Add("/dev/uinput");
            scenario._existingPaths.Add("/dev/input/event0");
            scenario._writablePaths.Add("/dev/uinput");
        }

        return scenario;
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
        return path is "/dev/input/event0" && _existingPaths.Contains(path);
    }

    public string[] GetInputEventCandidates()
    {
        return _existingPaths.Contains("/dev/input/event0") ? ["/dev/input/event0"] : [];
    }

    public string? GetEnvironmentVariable(string key)
    {
        return key is "XDG_SESSION_TYPE" ? SessionType : null;
    }

    public bool ProbeDaemonHandshake(string socketPath)
    {
        if (SocketProbePermissionDenied && string.Equals(socketPath, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal))
        {
            return false;
        }

        return DaemonHandshakeSucceeds;
    }

    public LinuxDaemonSocketAccessResult ProbeDaemonSocketAccess(string socketPath)
    {
        if (_socketAccessStatus is LinuxDaemonSocketAccessStatus.Missing)
        {
            return LinuxDaemonSocketAccessResult.Missing(socketPath);
        }

        var membership = new LinuxDaemonGroupMembershipResult(
            "crossmacro",
            _groupMembershipStatus,
            GroupId: 4242,
            UserName: "desktop-user",
            UserId: 1000,
            CurrentProcessGroupIds: [1000, 4242]);

        return new LinuxDaemonSocketAccessResult(
            socketPath,
            _socketAccessStatus,
            _groupMembershipStatus,
            new LinuxDaemonSocketMetadata(socketPath, LinuxFileSystemEntryKind.Socket, 980, 4242, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite),
            membership,
            _socketAccessStatus is LinuxDaemonSocketAccessStatus.PermissionDenied ? "Permission denied" : null,
            _socketAccessStatus is LinuxDaemonSocketAccessStatus.PermissionDenied ? new UnauthorizedAccessException("Permission denied") : null);
    }

    public LinuxDaemonHandshakeProbeResult ProbeDaemonHandshakeDiagnostic(string socketPath, TimeSpan timeout)
    {
        return _handshakeStatus is LinuxDaemonHandshakeStatus.Success
            ? LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(socketPath, timeout, _handshakeStatus, $"{_handshakeStatus} handshake failure");
    }
}
