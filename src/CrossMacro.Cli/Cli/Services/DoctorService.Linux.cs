using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Cli.Services;

public sealed partial class DoctorService
{
    private const string LinuxUInputPrimaryPath = "/dev/uinput";
    private const string LinuxUInputAlternatePath = "/dev/input/uinput";
    private const string LinuxDaemonRequiredGroup = "crossmacro";
    private static readonly TimeSpan LinuxDaemonHandshakeTimeout = TimeSpan.FromSeconds(2);

    private DoctorCheck BuildLinuxDisplayVariablesCheck(bool verbose)
    {
        var xdgSessionType = _getEnvironmentVariable("XDG_SESSION_TYPE");
        var display = _getEnvironmentVariable("DISPLAY");
        var waylandDisplay = _getEnvironmentVariable("WAYLAND_DISPLAY");

        var hasAnyDisplayVariable = !string.IsNullOrWhiteSpace(display) || !string.IsNullOrWhiteSpace(waylandDisplay);
        var hasSessionType = !string.IsNullOrWhiteSpace(xdgSessionType);

        var status = hasAnyDisplayVariable && hasSessionType
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-display-vars",
            Status = status,
            Message = status == DoctorCheckStatus.Pass
                ? "Linux display session variables look healthy."
                : "Some Linux display variables are missing; remote/SSH playback may fail.",
            Details = verbose
                ? new { xdgSessionType, display, waylandDisplay }
                : null
        };
    }

    private DoctorCheck BuildLinuxDaemonSocketCheck(LinuxInputState state, bool verbose)
    {
        var status = state.SocketAccess.Status == LinuxDaemonSocketAccessStatus.Accessible
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-daemon-socket",
            Status = status,
            Message = state.SocketAccess.Status switch
            {
                LinuxDaemonSocketAccessStatus.Accessible => "Daemon socket detected and accessible.",
                LinuxDaemonSocketAccessStatus.PermissionDenied => "Daemon socket detected but access was denied.",
                LinuxDaemonSocketAccessStatus.WrongType => "Daemon socket path exists but is not a Unix socket.",
                LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale => "Daemon socket exists but refused the connection or is stale.",
                LinuxDaemonSocketAccessStatus.Timeout => "Daemon socket exists but access probe timed out.",
                LinuxDaemonSocketAccessStatus.UnexpectedError => "Daemon socket probe failed unexpectedly.",
                _ => state.DirectFallbackAvailable
                    ? "Daemon socket not found, but direct input fallback is available so daemon is optional."
                    : "Daemon socket not found. Configure daemon mode or grant direct input access."
            },
            Details = verbose
                ? new
                {
                    defaultSocketPath = IpcProtocol.DefaultSocketPath,
                    defaultSocketExists = state.DefaultSocketExists,
                    socketPath = state.ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath,
                    socketStatus = state.SocketAccess.Status.ToString(),
                    failureKind = GetSocketFailureKind(state.SocketAccess),
                    directFallbackAvailable = state.DirectFallbackAvailable,
                    remediation = GetSocketRemediation(state)
                }
                : null
        };
    }

    private DoctorCheck BuildLinuxDaemonAccessCheck(LinuxInputState state, bool verbose)
    {
        var status = state.SocketAccess.Status switch
        {
            LinuxDaemonSocketAccessStatus.Accessible => DoctorCheckStatus.Pass,
            LinuxDaemonSocketAccessStatus.Missing => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            _ => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail
        };

        return new DoctorCheck
        {
            Name = "linux-daemon-access",
            Status = status,
            Message = state.SocketAccess.Status switch
            {
                LinuxDaemonSocketAccessStatus.Accessible => "Daemon IPC socket is accessible.",
                LinuxDaemonSocketAccessStatus.Missing => state.DirectFallbackAvailable
                    ? "Daemon IPC socket is missing; direct input fallback is available."
                    : "Daemon IPC socket is missing and direct input fallback is unavailable.",
                LinuxDaemonSocketAccessStatus.PermissionDenied => "Daemon IPC socket is present but the current user cannot access it.",
                LinuxDaemonSocketAccessStatus.WrongType => "Daemon IPC path is not a Unix socket.",
                LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale => "Daemon IPC socket refused the connection or is stale.",
                LinuxDaemonSocketAccessStatus.Timeout => "Daemon IPC access probe timed out.",
                _ => "Daemon IPC access probe failed unexpectedly."
            },
            Details = verbose ? BuildDaemonDetails(state, GetSocketFailureKind(state.SocketAccess)) : null
        };
    }

    private DoctorCheck BuildLinuxDaemonGroupCheck(LinuxInputState state, bool verbose)
    {
        var status = state.GroupMembershipStatus switch
        {
            LinuxDaemonGroupMembershipStatus.Member => DoctorCheckStatus.Pass,
            LinuxDaemonGroupMembershipStatus.Unknown when !state.DaemonSocketExists => DoctorCheckStatus.Warn,
            _ => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail
        };

        return new DoctorCheck
        {
            Name = "linux-daemon-group",
            Status = status,
            Message = state.GroupMembershipStatus switch
            {
                LinuxDaemonGroupMembershipStatus.Member => "Current session has daemon socket group membership.",
                LinuxDaemonGroupMembershipStatus.MissingGroup => "Required daemon socket group is missing.",
                LinuxDaemonGroupMembershipStatus.UserNotMember => "Current user is not in the daemon socket group.",
                LinuxDaemonGroupMembershipStatus.StaleSession => "Current user is configured for the daemon socket group, but this login session has not picked it up.",
                _ => state.DaemonSocketExists
                    ? "Daemon socket group membership could not be determined."
                    : "Daemon socket group membership was not checked because the daemon socket is missing."
            },
            Details = verbose ? BuildDaemonDetails(state, GetGroupFailureKind(state.GroupMembershipStatus)) : null
        };
    }

    private DoctorCheck BuildLinuxUInputCheck(LinuxInputState state, bool verbose)
    {
        var status = state.UInputWritable || state.DaemonHandshakeOk
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-uinput",
            Status = status,
            Message = state.UInputWritable
                ? "uinput appears writable."
                : state.DaemonHandshakeOk
                    ? "uinput is not writable; daemon mode should be used."
                    : state.DaemonSocketExists
                        ? "uinput is not writable and daemon handshake failed."
                        : "uinput is not writable and daemon socket is missing.",
            Details = verbose
                ? new
                {
                    uInputPrimary = LinuxUInputPrimaryPath,
                    primaryExists = state.PrimaryUInputExists,
                    canWritePrimary = state.CanWritePrimary,
                    uInputAlternate = LinuxUInputAlternatePath,
                    alternateExists = state.AlternateUInputExists,
                    canWriteAlternate = state.CanWriteAlternate,
                    directFallbackAvailable = state.DirectFallbackAvailable
                }
                : null
        };
    }

    private DoctorCheck BuildLinuxInputReadinessCheck(LinuxInputState state, bool verbose)
    {
        DoctorCheckStatus status;
        string message;

        if (state.IsWayland)
        {
            if (state.DirectFallbackAvailable)
            {
                status = DoctorCheckStatus.Pass;
                message = "Wayland input is ready via direct input fallback. Daemon is not required.";
            }
            else if (state.DaemonHandshakeOk)
            {
                status = DoctorCheckStatus.Pass;
                message = "Wayland input is ready via daemon socket.";
            }
            else if (state.DaemonSocketExists)
            {
                status = DoctorCheckStatus.Fail;
                message = "Daemon socket is present but handshake failed. Restart daemon/service permissions.";
            }
            else
            {
                status = DoctorCheckStatus.Fail;
                message = "Wayland requires either daemon socket access or direct input fallback.";
            }
        }
        else if (state.IsX11)
        {
            if (state.DirectFallbackAvailable || state.DaemonHandshakeOk)
            {
                status = DoctorCheckStatus.Pass;
                message = "Linux input backend looks ready for X11.";
            }
            else
            {
                status = DoctorCheckStatus.Warn;
                message = "No daemon socket and no writable uinput detected. Input simulation may fail.";
            }
        }
        else
        {
            if (state.DirectFallbackAvailable || state.DaemonHandshakeOk)
            {
                status = DoctorCheckStatus.Pass;
                message = "Linux input backend looks ready.";
            }
            else
            {
                status = DoctorCheckStatus.Warn;
                message = "Linux session type is unclear and no input backend was detected.";
            }
        }

        return new DoctorCheck
        {
            Name = "linux-input-readiness",
            Status = status,
            Message = message,
            Details = verbose
                ? new
                {
                    state.SessionType,
                    state.IsWayland,
                    state.IsX11,
                    state.IsFlatpak,
                    daemonSocketExists = state.DaemonSocketExists,
                    daemonHandshakeOk = state.DaemonHandshakeOk,
                    uInputWritable = state.UInputWritable,
                    directFallbackAvailable = state.DirectFallbackAvailable
                }
                : null
        };
    }

    private DoctorCheck BuildLinuxDaemonHandshakeCheck(LinuxInputState state, bool verbose)
    {
        DoctorCheckStatus status;
        string message;

        if (!state.DaemonSocketExists)
        {
            status = state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail;
            message = state.DirectFallbackAvailable
                ? "Daemon socket not present; skipped handshake probe (direct input fallback is available)."
                : "Daemon socket not present; handshake probe skipped.";
        }
        else if (state.DaemonHandshakeOk)
        {
            status = DoctorCheckStatus.Pass;
            message = "Daemon handshake probe succeeded.";
        }
        else
        {
            status = state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail;
            message = state.DirectFallbackAvailable
                ? "Daemon socket exists but handshake probe failed. Direct input fallback may still work."
                : "Daemon socket exists but handshake probe failed.";
        }

        return new DoctorCheck
        {
            Name = "linux-daemon-handshake",
            Status = status,
            Message = message,
            Details = verbose
                ? BuildDaemonDetails(state, GetHandshakeFailureKind(state.Handshake.Status))
                : null
        };
    }

    private DoctorCheck BuildLinuxGsrCompatibilityCheck(LinuxInputState state, bool verbose)
    {
        return new DoctorCheck
        {
            Name = "linux-gsr-compatibility",
            Status = state.GsrVirtualKeyboardDetected ? DoctorCheckStatus.Warn : DoctorCheckStatus.Pass,
            Message = state.GsrVirtualKeyboardDetected
                ? "GPU Screen Recorder UI virtual keyboard is active. CrossMacro can read it, but GSR-owned hotkeys may still be swallowed by GSR."
                : "GPU Screen Recorder UI virtual keyboard was not detected.",
            Details = verbose
                ? new
                {
                    inputDevicesPath = LinuxGsrCompatibility.InputDevicesPath,
                    gsrVirtualKeyboardDetected = state.GsrVirtualKeyboardDetected,
                    matchedDeviceName = state.GsrVirtualKeyboardDetected ? LinuxGsrCompatibility.VirtualKeyboardName : null
                }
                : null
        };
    }

    private LinuxInputState BuildLinuxInputState()
    {
        var socketAccess = _daemonSocketAccessProbe(IpcProtocol.DefaultSocketPath);
        var resolvedSocketPath = socketAccess.Status == LinuxDaemonSocketAccessStatus.Missing
            ? null
            : socketAccess.SocketPath;
        var defaultSocketExists = socketAccess.Status != LinuxDaemonSocketAccessStatus.Missing;
        var daemonSocketExists = resolvedSocketPath is not null;

        var primaryExists = _fileExists(LinuxUInputPrimaryPath);
        var alternateExists = _fileExists(LinuxUInputAlternatePath);
        var canWritePrimary = primaryExists && _canOpenForWrite(LinuxUInputPrimaryPath);
        var canWriteAlternate = alternateExists && _canOpenForWrite(LinuxUInputAlternatePath);
        var uInputWritable = canWritePrimary || canWriteAlternate;
        var canReadInputEvents = HasReadableInputEventAccess();
        var directFallback = LinuxDirectInputFallbackResult.FromAccess(uInputWritable, canReadInputEvents);

        var handshake = daemonSocketExists && !string.IsNullOrWhiteSpace(resolvedSocketPath)
            ? _daemonHandshakeDiagnosticProbe(resolvedSocketPath, LinuxDaemonHandshakeTimeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                IpcProtocol.DefaultSocketPath,
                LinuxDaemonHandshakeTimeout,
                LinuxDaemonHandshakeStatus.MissingSocket,
                "Daemon socket is missing.");

        var daemonHandshakeOk = handshake.Succeeded;

        var sessionType = _getEnvironmentVariable("XDG_SESSION_TYPE");
        var isWaylandSession = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
        var isX11Session = string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase);
        var detectedEnvironment = _environmentInfoProvider.CurrentEnvironment;

        var isWayland = isWaylandSession
            || detectedEnvironment == DisplayEnvironment.LinuxWayland
            || detectedEnvironment == DisplayEnvironment.LinuxHyprland
            || detectedEnvironment == DisplayEnvironment.LinuxWayfire
            || detectedEnvironment == DisplayEnvironment.LinuxKDE
            || detectedEnvironment == DisplayEnvironment.LinuxGnome;

        var isX11 = isX11Session || detectedEnvironment == DisplayEnvironment.LinuxX11;

        var isFlatpak = !string.IsNullOrWhiteSpace(_getEnvironmentVariable("FLATPAK_ID"));
        var gsrVirtualKeyboardDetected = LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(
            _readAllTextIfExists(LinuxGsrCompatibility.InputDevicesPath));

        return new LinuxInputState(
            SessionType: sessionType,
            IsWayland: isWayland,
            IsX11: isX11,
            IsFlatpak: isFlatpak,
            DefaultSocketExists: defaultSocketExists,
            DaemonSocketExists: daemonSocketExists,
            ResolvedSocketPath: resolvedSocketPath,
            SocketAccess: socketAccess,
            GroupMembershipStatus: socketAccess.GroupMembershipStatus,
            DirectFallback: directFallback,
            Handshake: handshake,
            DaemonHandshakeOk: daemonHandshakeOk,
            PrimaryUInputExists: primaryExists,
            AlternateUInputExists: alternateExists,
            CanWritePrimary: canWritePrimary,
            CanWriteAlternate: canWriteAlternate,
            UInputWritable: uInputWritable,
            GsrVirtualKeyboardDetected: gsrVirtualKeyboardDetected);
    }

    private bool HasReadableInputEventAccess()
    {
        return _inputDeviceAccessProbe.HasUsableReadableInputDevices();
    }

    private string? ResolveAvailableSocketPath()
    {
        return _fileExists(IpcProtocol.DefaultSocketPath)
            ? IpcProtocol.DefaultSocketPath
            : null;
    }

    private LinuxDaemonSocketAccessResult ProbeDaemonSocketAccess(string socketPath)
    {
        if (!_fileExists(socketPath))
        {
            return LinuxDaemonSocketAccessResult.Missing(socketPath);
        }

        if (_daemonHandshakeProbe(socketPath))
        {
            return LinuxDaemonSocketAccessResult.Accessible(socketPath);
        }

        return new LinuxDaemonSocketAccessResult(
            socketPath,
            LinuxDaemonSocketAccessStatus.UnexpectedError,
            Message: "Legacy daemon handshake probe failed before structured socket access diagnostics were available.");
    }

    private LinuxDaemonHandshakeProbeResult ProbeDaemonHandshakeDiagnostic(string socketPath, TimeSpan timeout)
    {
        return _daemonHandshakeProbe(socketPath)
            ? LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                socketPath,
                timeout,
                LinuxDaemonHandshakeStatus.UnexpectedError,
                "Legacy daemon handshake probe failed before structured handshake diagnostics were available.");
    }

    private static bool ProbeDaemonHandshake(string socketPath)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static object BuildDaemonDetails(LinuxInputState state, string? failureKind)
    {
        var membership = state.SocketAccess.GroupMembership;
        return new
        {
            socketPath = state.ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath,
            socketStatus = state.SocketAccess.Status.ToString(),
            handshakeStatus = state.Handshake.Status.ToString(),
            failureKind,
            requiredGroup = LinuxDaemonRequiredGroup,
            currentUser = membership?.UserName,
            currentUid = membership?.UserId,
            currentProcessGroups = membership?.CurrentProcessGroupIds ?? Array.Empty<int>(),
            groupDatabaseContainsUser = state.GroupDatabaseContainsUser,
            currentSessionHasGroup = state.CurrentSessionHasGroup,
            remediation = GetDaemonRemediation(state, failureKind),
            directFallbackAvailable = state.DirectFallbackAvailable,
            message = state.SocketAccess.Message ?? state.Handshake.Message ?? membership?.Message
        };
    }

    private static string? GetSocketFailureKind(LinuxDaemonSocketAccessResult socketAccess)
    {
        return socketAccess.Status == LinuxDaemonSocketAccessStatus.Accessible
            ? null
            : socketAccess.Status.ToString();
    }

    private static string? GetHandshakeFailureKind(LinuxDaemonHandshakeStatus status)
    {
        return status == LinuxDaemonHandshakeStatus.Success ? null : status.ToString();
    }

    private static string? GetGroupFailureKind(LinuxDaemonGroupMembershipStatus status)
    {
        return status == LinuxDaemonGroupMembershipStatus.Member ? null : status.ToString();
    }

    private static string GetSocketRemediation(LinuxInputState state)
    {
        return GetDaemonRemediation(state, GetSocketFailureKind(state.SocketAccess));
    }

    private static string GetDaemonRemediation(LinuxInputState state, string? failureKind)
    {
        if (state.GroupMembershipStatus == LinuxDaemonGroupMembershipStatus.StaleSession)
        {
            return "Log out and back in, or reboot, so the current session picks up crossmacro group membership.";
        }

        if (state.GroupMembershipStatus == LinuxDaemonGroupMembershipStatus.UserNotMember)
        {
            return "Run `sudo usermod -aG crossmacro $USER`, then log out and back in or reboot.";
        }

        if (state.GroupMembershipStatus == LinuxDaemonGroupMembershipStatus.MissingGroup)
        {
            return "Reinstall or repair the CrossMacro daemon package so the crossmacro system group is created.";
        }

        return failureKind switch
        {
            nameof(LinuxDaemonSocketAccessStatus.Missing) or nameof(LinuxDaemonHandshakeStatus.MissingSocket) =>
                "Start the CrossMacro daemon with `sudo systemctl enable --now crossmacro.service`, or use a direct input fallback channel.",
            nameof(LinuxDaemonSocketAccessStatus.PermissionDenied) or nameof(LinuxDaemonHandshakeStatus.PermissionDenied) =>
                "Ensure the user is in the crossmacro group, then log out and back in or reboot.",
            nameof(LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale) or nameof(LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale) =>
                "Restart the CrossMacro daemon with `sudo systemctl restart crossmacro.service`.",
            nameof(LinuxDaemonSocketAccessStatus.Timeout) or nameof(LinuxDaemonHandshakeStatus.Timeout) =>
                "Restart the CrossMacro daemon and inspect `journalctl -u crossmacro.service` for hangs.",
            nameof(LinuxDaemonHandshakeStatus.ProtocolMismatch) =>
                "Update CrossMacro CLI and daemon packages together so their IPC protocol versions match.",
            nameof(LinuxDaemonHandshakeStatus.HandshakeRejected) =>
                "Check daemon authorization policy and `journalctl -u crossmacro.service` for rejected client details.",
            _ => "Inspect `systemctl status crossmacro.service` and `journalctl -u crossmacro.service` for daemon IPC errors."
        };
    }

    private sealed record LinuxInputState(
        string? SessionType,
        bool IsWayland,
        bool IsX11,
        bool IsFlatpak,
        bool DefaultSocketExists,
        bool DaemonSocketExists,
        string? ResolvedSocketPath,
        LinuxDaemonSocketAccessResult SocketAccess,
        LinuxDaemonGroupMembershipStatus GroupMembershipStatus,
        LinuxDirectInputFallbackResult DirectFallback,
        LinuxDaemonHandshakeProbeResult Handshake,
        bool DaemonHandshakeOk,
        bool PrimaryUInputExists,
        bool AlternateUInputExists,
        bool CanWritePrimary,
        bool CanWriteAlternate,
        bool UInputWritable,
        bool GsrVirtualKeyboardDetected)
    {
        public bool DirectFallbackAvailable => DirectFallback.IsAvailable;

        public bool CurrentSessionHasGroup => GroupMembershipStatus == LinuxDaemonGroupMembershipStatus.Member;

        public bool GroupDatabaseContainsUser => GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.Member or LinuxDaemonGroupMembershipStatus.StaleSession;
    }
}
