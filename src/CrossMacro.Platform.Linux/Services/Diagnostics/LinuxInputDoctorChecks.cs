using System.Text.Json.Nodes;

namespace CrossMacro.Platform.Linux.Services.Diagnostics;

/// <summary>The doctor checks derived from one Linux input diagnostic snapshot: daemon IPC, uinput, readiness and GSR compatibility.</summary>
internal static class LinuxInputDoctorChecks
{
    private const string UInputPrimaryPath = "/dev/uinput";
    private const string UInputAlternatePath = "/dev/input/uinput";

    public static IEnumerable<DoctorCheck> Build(LinuxInputState state, IReadOnlyList<DoctorCheck> capabilityChecks, bool verbose)
    {
        if (state.DaemonDiagnosticsEnabled)
        {
            yield return BuildDaemonSocketCheck(state, verbose);
            yield return BuildDaemonAccessCheck(state, verbose);
            yield return BuildDaemonGroupCheck(state, verbose);
            yield return BuildDaemonHandshakeCheck(state, verbose);
        }

        yield return BuildUInputCheck(state, verbose);
        yield return BuildInputReadinessCheck(state, capabilityChecks, verbose);
        yield return BuildGsrCompatibilityCheck(state, verbose);
    }

    private static DoctorCheck BuildDaemonSocketCheck(LinuxInputState state, bool verbose)
    {
        var status = state.SocketAccess.Status is LinuxDaemonSocketAccessStatus.Accessible
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-daemon-socket",
            Status = status,
            Message = state.SocketAccess.Status switch
            {
                LinuxDaemonSocketAccessStatus.Accessible => "Daemon socket detected and accessible.",
                LinuxDaemonSocketAccessStatus.Missing => state.DirectFallbackAvailable
                    ? "Daemon socket not found, but direct input fallback is available so daemon is optional."
                    : "Daemon socket not found. Configure daemon mode or grant direct input access.",
                LinuxDaemonSocketAccessStatus.PermissionDenied => "Daemon socket detected but access was denied.",
                LinuxDaemonSocketAccessStatus.WrongType => "Daemon socket path exists but is not a Unix socket.",
                LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale => "Daemon socket exists but refused the connection or is stale.",
                LinuxDaemonSocketAccessStatus.Timeout => "Daemon socket exists but access probe timed out.",
                LinuxDaemonSocketAccessStatus.UnexpectedError => "Daemon socket probe failed unexpectedly.",
                _ => state.DirectFallbackAvailable
                    ? "Daemon socket not found, but direct input fallback is available so daemon is optional."
                    : "Daemon socket not found. Configure daemon mode or grant direct input access.",
            },
            Details = verbose
                ? new JsonObject
                {
                    ["defaultSocketPath"] = IpcProtocol.DefaultSocketPath,
                    ["defaultSocketExists"] = state.DefaultSocketExists,
                    ["socketPath"] = state.ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath,
                    ["socketStatus"] = state.SocketAccess.Status.ToString(),
                    ["failureKind"] = GetSocketFailureKind(state.SocketAccess),
                    ["directFallbackAvailable"] = state.DirectFallbackAvailable,
                    ["remediation"] = GetSocketRemediation(state),
                }
                : null,
        };
    }

    private static DoctorCheck BuildDaemonAccessCheck(LinuxInputState state, bool verbose)
    {
        var status = state.SocketAccess.Status switch
        {
            LinuxDaemonSocketAccessStatus.Accessible => DoctorCheckStatus.Pass,
            LinuxDaemonSocketAccessStatus.Missing => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonSocketAccessStatus.PermissionDenied => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonSocketAccessStatus.WrongType => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonSocketAccessStatus.ConnectionRefusedOrStale => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonSocketAccessStatus.Timeout => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonSocketAccessStatus.UnexpectedError => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            _ => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
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
                LinuxDaemonSocketAccessStatus.UnexpectedError => "Daemon IPC access probe failed unexpectedly.",
                _ => "Daemon IPC access probe failed unexpectedly.",
            },
            Details = verbose ? BuildDaemonDetails(state, GetSocketFailureKind(state.SocketAccess)) : null,
        };
    }

    private static DoctorCheck BuildDaemonGroupCheck(LinuxInputState state, bool verbose)
    {
        var status = state.GroupMembershipStatus switch
        {
            LinuxDaemonGroupMembershipStatus.Member => DoctorCheckStatus.Pass,
            LinuxDaemonGroupMembershipStatus.Unknown when !state.DaemonSocketExists => DoctorCheckStatus.Warn,
            LinuxDaemonGroupMembershipStatus.MissingGroup => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonGroupMembershipStatus.UserNotMember => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonGroupMembershipStatus.StaleSession => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            LinuxDaemonGroupMembershipStatus.Unknown => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
            _ => state.DirectFallbackAvailable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail,
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
                LinuxDaemonGroupMembershipStatus.Unknown => state.DaemonSocketExists
                    ? "Daemon socket group membership could not be determined."
                    : "Daemon socket group membership was not checked because the daemon socket is missing.",
                _ => state.DaemonSocketExists
                    ? "Daemon socket group membership could not be determined."
                    : "Daemon socket group membership was not checked because the daemon socket is missing.",
            },
            Details = verbose ? BuildDaemonDetails(state, GetGroupFailureKind(state.GroupMembershipStatus)) : null,
        };
    }

    private static DoctorCheck BuildDaemonHandshakeCheck(LinuxInputState state, bool verbose)
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
                : null,
        };
    }

    private static DoctorCheck BuildUInputCheck(LinuxInputState state, bool verbose)
    {
        var status = state.UInputWritable || state.DaemonHandshakeOk
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        string message;
        if (state.UInputWritable)
        {
            message = "uinput appears writable.";
        }
        else if (state.DaemonHandshakeOk)
        {
            message = "uinput is not writable; daemon mode should be used.";
        }
        else if (!state.DaemonDiagnosticsEnabled)
        {
            message = "uinput is not writable in this direct-device package.";
        }
        else if (state.DaemonSocketExists)
        {
            message = "uinput is not writable and daemon handshake failed.";
        }
        else
        {
            message = "uinput is not writable and daemon socket is missing.";
        }

        return new DoctorCheck
        {
            Name = "linux-uinput",
            Status = status,
            Message = message,
            Details = verbose
                ? new JsonObject
                {
                    ["uInputPrimary"] = UInputPrimaryPath,
                    ["primaryExists"] = state.PrimaryUInputExists,
                    ["canWritePrimary"] = state.CanWritePrimary,
                    ["uInputAlternate"] = UInputAlternatePath,
                    ["alternateExists"] = state.AlternateUInputExists,
                    ["canWriteAlternate"] = state.CanWriteAlternate,
                    ["directFallbackAvailable"] = state.DirectFallbackAvailable,
                }
                : null,
        };
    }

    private static DoctorCheck BuildInputReadinessCheck(
        LinuxInputState state,
        IReadOnlyList<DoctorCheck> capabilityChecks,
        bool verbose)
    {
        DoctorCheckStatus status;
        string message;

        if (state.IsWayland)
        {
            if (state.DirectFallbackAvailable)
            {
                status = DoctorCheckStatus.Pass;
                message = state.DaemonDiagnosticsEnabled
                    ? "Wayland input is ready via direct input fallback. Daemon is not required."
                    : "Wayland input is ready via direct devices.";
            }
            else if (state.DaemonHandshakeOk)
            {
                status = DoctorCheckStatus.Pass;
                message = "Wayland input is ready via daemon socket.";
            }
            else if (!state.DaemonDiagnosticsEnabled)
            {
                status = DoctorCheckStatus.Fail;
                message = "Wayland direct-device input requires writable uinput and readable input event devices.";
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
            if (capabilityChecks.Any(check => check.Name is "input-simulator" && check.Status is DoctorCheckStatus.Pass)
                && capabilityChecks.Any(check => check.Name is "input-capture" && check.Status is DoctorCheckStatus.Pass))
            {
                status = DoctorCheckStatus.Pass;
                message = "Input simulation and capture backends are ready for X11.";
            }
            else
            {
                status = DoctorCheckStatus.Warn;
                message = "X11 input is not fully available. See the input-simulator and input-capture checks.";
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
                ? new JsonObject
                {
                    ["sessionType"] = state.SessionType,
                    ["isWayland"] = state.IsWayland,
                    ["isX11"] = state.IsX11,
                    ["isFlatpak"] = state.IsFlatpak,
                    ["daemonSocketExists"] = state.DaemonSocketExists,
                    ["daemonHandshakeOk"] = state.DaemonHandshakeOk,
                    ["uInputWritable"] = state.UInputWritable,
                    ["directFallbackAvailable"] = state.DirectFallbackAvailable,
                }
                : null,
        };
    }

    private static DoctorCheck BuildGsrCompatibilityCheck(LinuxInputState state, bool verbose)
    {
        JsonObject? details = null;
        if (verbose)
        {
            details = new JsonObject
            {
                ["inputDevicesPath"] = LinuxGsrCompatibility.InputDevicesPath,
                ["gsrVirtualKeyboardDetected"] = state.GsrVirtualKeyboardDetected,
                ["matchedDeviceName"] = state.GsrVirtualKeyboardDetected ? LinuxGsrCompatibility.VirtualKeyboardName : null,
            };
        }

        return new DoctorCheck
        {
            Name = "linux-gsr-compatibility",
            Status = state.GsrVirtualKeyboardDetected ? DoctorCheckStatus.Warn : DoctorCheckStatus.Pass,
            Message = state.GsrVirtualKeyboardDetected
                ? "GPU Screen Recorder UI virtual keyboard is active. CrossMacro can read it, but GSR-owned hotkeys may still be swallowed by GSR."
                : "GPU Screen Recorder UI virtual keyboard was not detected.",
            Details = details,
        };
    }

    private static JsonObject BuildDaemonDetails(LinuxInputState state, string? failureKind)
    {
        var membership = state.SocketAccess.GroupMembership;
        var processGroups = membership?.CurrentProcessGroupIds ?? [];
        var processGroupsArray = new JsonArray(processGroups.Select(id => (JsonNode)id).ToArray());

        return new JsonObject
        {
            ["socketPath"] = state.ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath,
            ["socketStatus"] = state.SocketAccess.Status.ToString(),
            ["handshakeStatus"] = state.Handshake.Status.ToString(),
            ["failureKind"] = failureKind,
            ["requiredGroup"] = LinuxInputDiagnosticSnapshot.DaemonGroupName,
            ["currentUser"] = membership?.UserName,
            ["currentUid"] = membership?.UserId,
            ["currentProcessGroups"] = processGroupsArray,
            ["groupDatabaseContainsUser"] = state.GroupDatabaseContainsUser,
            ["currentSessionHasGroup"] = state.CurrentSessionHasGroup,
            ["remediation"] = GetDaemonRemediation(state, failureKind),
            ["directFallbackAvailable"] = state.DirectFallbackAvailable,
            ["message"] = state.SocketAccess.Message ?? state.Handshake.Message ?? membership?.Message,
        };
    }

    private static string? GetSocketFailureKind(LinuxDaemonSocketAccessResult socketAccess)
    {
        return socketAccess.Status is LinuxDaemonSocketAccessStatus.Accessible
            ? null
            : socketAccess.Status.ToString();
    }

    private static string? GetHandshakeFailureKind(LinuxDaemonHandshakeStatus status)
    {
        return status is LinuxDaemonHandshakeStatus.Success ? null : status.ToString();
    }

    private static string? GetGroupFailureKind(LinuxDaemonGroupMembershipStatus status)
    {
        return status is LinuxDaemonGroupMembershipStatus.Member ? null : status.ToString();
    }

    private static string GetSocketRemediation(LinuxInputState state)
    {
        return GetDaemonRemediation(state, GetSocketFailureKind(state.SocketAccess));
    }

    private static string GetDaemonRemediation(LinuxInputState state, string? failureKind)
    {
        if (state.GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.StaleSession)
        {
            return "Log out and back in, or reboot, so the current session picks up crossmacro group membership.";
        }

        if (state.GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.UserNotMember)
        {
            return "Run `sudo gpasswd -a $USER crossmacro`, then log out and back in or reboot.";
        }

        if (state.GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.MissingGroup)
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
            _ => "Inspect `systemctl status crossmacro.service` and `journalctl -u crossmacro.service` for daemon IPC errors.",
        };
    }
}
