using System;
using System.IO;
using System.Net.Sockets;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed partial class DoctorService
{
    private const string LinuxUInputPrimaryPath = "/dev/uinput";
    private const string LinuxUInputAlternatePath = "/dev/input/uinput";

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
        var status = state.DaemonSocketExists || state.UInputWritable
            ? DoctorCheckStatus.Pass
            : DoctorCheckStatus.Warn;

        return new DoctorCheck
        {
            Name = "linux-daemon-socket",
            Status = status,
            Message = state.DaemonSocketExists
                ? "Daemon socket detected."
                : state.UInputWritable
                    ? "Daemon socket not found, but direct uinput access is available so daemon is optional."
                    : "Daemon socket not found. Configure daemon mode or grant direct uinput access.",
            Details = verbose
                ? new
                {
                    defaultSocketPath = IpcProtocol.DefaultSocketPath,
                    defaultSocketExists = state.DefaultSocketExists,
                    fallbackSocketPath = IpcProtocol.FallbackSocketPath,
                    fallbackSocketExists = state.FallbackSocketExists
                }
                : null
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
                    canWriteAlternate = state.CanWriteAlternate
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
            if (state.UInputWritable)
            {
                status = DoctorCheckStatus.Pass;
                message = "Wayland input is ready via direct uinput access. Daemon is not required.";
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
                message = "Wayland requires either daemon socket access or writable uinput.";
            }
        }
        else if (state.IsX11)
        {
            if (state.UInputWritable || state.DaemonHandshakeOk)
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
            if (state.UInputWritable || state.DaemonHandshakeOk)
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
                    uInputWritable = state.UInputWritable
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
            status = state.UInputWritable ? DoctorCheckStatus.Pass : DoctorCheckStatus.Warn;
            message = state.UInputWritable
                ? "Daemon socket not present; skipped handshake probe (direct uinput is available)."
                : "Daemon socket not present; handshake probe skipped.";
        }
        else if (state.DaemonHandshakeOk)
        {
            status = DoctorCheckStatus.Pass;
            message = "Daemon handshake probe succeeded.";
        }
        else
        {
            status = state.UInputWritable ? DoctorCheckStatus.Warn : DoctorCheckStatus.Fail;
            message = state.UInputWritable
                ? "Daemon socket exists but handshake probe failed. Playback may still work via direct uinput."
                : "Daemon socket exists but handshake probe failed.";
        }

        return new DoctorCheck
        {
            Name = "linux-daemon-handshake",
            Status = status,
            Message = message,
            Details = verbose
                ? new
                {
                    socketPath = state.ResolvedSocketPath,
                    daemonSocketExists = state.DaemonSocketExists,
                    daemonHandshakeOk = state.DaemonHandshakeOk
                }
                : null
        };
    }

    private LinuxInputState BuildLinuxInputState()
    {
        var defaultSocketExists = _fileExists(IpcProtocol.DefaultSocketPath);
        var fallbackSocketExists = _fileExists(IpcProtocol.FallbackSocketPath);
        var daemonSocketExists = defaultSocketExists || fallbackSocketExists;
        var resolvedSocketPath = defaultSocketExists
            ? IpcProtocol.DefaultSocketPath
            : fallbackSocketExists
                ? IpcProtocol.FallbackSocketPath
                : null;
        var daemonHandshakeOk = daemonSocketExists
            && !string.IsNullOrWhiteSpace(resolvedSocketPath)
            && _daemonHandshakeProbe(resolvedSocketPath);

        var primaryExists = _fileExists(LinuxUInputPrimaryPath);
        var alternateExists = _fileExists(LinuxUInputAlternatePath);
        var canWritePrimary = primaryExists && _canOpenForWrite(LinuxUInputPrimaryPath);
        var canWriteAlternate = alternateExists && _canOpenForWrite(LinuxUInputAlternatePath);
        var uInputWritable = canWritePrimary || canWriteAlternate;

        var sessionType = _getEnvironmentVariable("XDG_SESSION_TYPE");
        var isWaylandSession = string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
        var isX11Session = string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase);
        var detectedEnvironment = _environmentInfoProvider.CurrentEnvironment;

        var isWayland = isWaylandSession
            || detectedEnvironment == DisplayEnvironment.LinuxWayland
            || detectedEnvironment == DisplayEnvironment.LinuxHyprland
            || detectedEnvironment == DisplayEnvironment.LinuxKDE
            || detectedEnvironment == DisplayEnvironment.LinuxGnome;

        var isX11 = isX11Session || detectedEnvironment == DisplayEnvironment.LinuxX11;

        var isFlatpak = !string.IsNullOrWhiteSpace(_getEnvironmentVariable("FLATPAK_ID"));

        return new LinuxInputState(
            SessionType: sessionType,
            IsWayland: isWayland,
            IsX11: isX11,
            IsFlatpak: isFlatpak,
            DefaultSocketExists: defaultSocketExists,
            FallbackSocketExists: fallbackSocketExists,
            DaemonSocketExists: daemonSocketExists,
            ResolvedSocketPath: resolvedSocketPath,
            DaemonHandshakeOk: daemonHandshakeOk,
            PrimaryUInputExists: primaryExists,
            AlternateUInputExists: alternateExists,
            CanWritePrimary: canWritePrimary,
            CanWriteAlternate: canWriteAlternate,
            UInputWritable: uInputWritable);
    }

    private static bool ProbeDaemonHandshake(string socketPath)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            {
                SendTimeout = 2000,
                ReceiveTimeout = 2000
            };

            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            using var stream = new NetworkStream(socket, ownsSocket: true);
            using var writer = new BinaryWriter(stream);
            using var reader = new BinaryReader(stream);

            writer.Write((byte)IpcOpCode.Handshake);
            writer.Write(IpcProtocol.ProtocolVersion);
            writer.Flush();

            var opcode = (IpcOpCode)reader.ReadByte();
            var version = reader.ReadInt32();
            return opcode == IpcOpCode.Handshake && version == IpcProtocol.ProtocolVersion;
        }
        catch
        {
            return false;
        }
    }

    private sealed record LinuxInputState(
        string? SessionType,
        bool IsWayland,
        bool IsX11,
        bool IsFlatpak,
        bool DefaultSocketExists,
        bool FallbackSocketExists,
        bool DaemonSocketExists,
        string? ResolvedSocketPath,
        bool DaemonHandshakeOk,
        bool PrimaryUInputExists,
        bool AlternateUInputExists,
        bool CanWritePrimary,
        bool CanWriteAlternate,
        bool UInputWritable);
}
