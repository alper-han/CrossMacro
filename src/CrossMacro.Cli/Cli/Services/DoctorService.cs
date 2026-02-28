using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class DoctorService : IDoctorService
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly IMousePositionProvider _mousePositionProvider;
    private readonly IPermissionChecker? _permissionChecker;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly Func<bool> _isLinux;
    private readonly Func<bool> _isWindows;
    private readonly Func<bool> _isMacOS;
    private readonly Func<string, bool> _daemonHandshakeProbe;

    public DoctorService(
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        IPermissionChecker? permissionChecker = null)
        : this(
            environmentInfoProvider,
            displaySessionService,
            Environment.GetEnvironmentVariable,
            File.Exists,
            CanOpenForWrite,
            inputSimulatorFactory,
            inputCaptureFactory,
            mousePositionProvider,
            permissionChecker,
            OperatingSystem.IsLinux,
            OperatingSystem.IsWindows,
            OperatingSystem.IsMacOS,
            ProbeDaemonHandshake)
    {
    }

    public DoctorService(
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        IPermissionChecker? permissionChecker = null,
        Func<bool>? isLinux = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        Func<string, bool>? daemonHandshakeProbe = null)
    {
        _environmentInfoProvider = environmentInfoProvider ?? throw new ArgumentNullException(nameof(environmentInfoProvider));
        _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputSimulatorFactory = inputSimulatorFactory ?? throw new ArgumentNullException(nameof(inputSimulatorFactory));
        _inputCaptureFactory = inputCaptureFactory ?? throw new ArgumentNullException(nameof(inputCaptureFactory));
        _mousePositionProvider = mousePositionProvider ?? throw new ArgumentNullException(nameof(mousePositionProvider));
        _permissionChecker = permissionChecker;
        _isLinux = isLinux ?? OperatingSystem.IsLinux;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
        _daemonHandshakeProbe = daemonHandshakeProbe ?? ProbeDaemonHandshake;
    }

    public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var checks = new List<DoctorCheck>
        {
            BuildPlatformCheck(),
            BuildEnvironmentCheck(),
            BuildConfigPathCheck(),
            BuildDisplaySessionCheck(),
            BuildInputSimulationCheck(verbose),
            BuildInputCaptureCheck(verbose),
            BuildPositionProviderCheck(verbose)
        };

        if (_isMacOS())
        {
            checks.Add(BuildMacOSAccessibilityCheck(verbose));
        }

        if (_isLinux())
        {
            var linuxState = BuildLinuxInputState();
            checks.Add(BuildLinuxDisplayVariablesCheck(verbose));
            checks.Add(BuildLinuxDaemonSocketCheck(linuxState, verbose));
            checks.Add(BuildLinuxDaemonHandshakeCheck(linuxState, verbose));
            checks.Add(BuildLinuxUInputCheck(linuxState, verbose));
            checks.Add(BuildLinuxInputReadinessCheck(linuxState, verbose));
        }

        return Task.FromResult(new DoctorReport
        {
            Checks = checks
        });
    }

    private DoctorCheck BuildPlatformCheck()
    {
        var description = RuntimeInformation.OSDescription;
        return new DoctorCheck
        {
            Name = "platform",
            Status = DoctorCheckStatus.Pass,
            Message = $"Platform: {description}",
            Details = new
            {
                osDescription = description,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            }
        };
    }

    private DoctorCheck BuildEnvironmentCheck()
    {
        return new DoctorCheck
        {
            Name = "display-environment",
            Status = DoctorCheckStatus.Pass,
            Message = $"Detected environment: {_environmentInfoProvider.CurrentEnvironment}",
            Details = new
            {
                currentEnvironment = _environmentInfoProvider.CurrentEnvironment.ToString(),
                wmHandlesCloseButton = _environmentInfoProvider.WindowManagerHandlesCloseButton
            }
        };
    }

    private DoctorCheck BuildConfigPathCheck()
    {
        var configDirectory = ResolveConfigDirectory();

        try
        {
            Directory.CreateDirectory(configDirectory);
            var isWritable = CanWriteDirectory(configDirectory);

            return new DoctorCheck
            {
                Name = "config-path",
                Status = isWritable ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isWritable
                    ? "Config directory is writable."
                    : "Config directory is not writable.",
                Details = new { configDirectory, writable = isWritable }
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheck
            {
                Name = "config-path",
                Status = DoctorCheckStatus.Fail,
                Message = "Failed to access config directory.",
                Details = new { configDirectory, error = ex.Message }
            };
        }
    }

    private static string ResolveConfigDirectory()
    {
        string configBase;

        if (OperatingSystem.IsMacOS())
        {
            configBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support");
        }
        else if (OperatingSystem.IsWindows())
        {
            configBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            configBase = string.IsNullOrWhiteSpace(xdgConfigHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdgConfigHome;
        }

        return Path.Combine(configBase, AppConstants.AppIdentifier);
    }

    private DoctorCheck BuildDisplaySessionCheck()
    {
        var supported = _displaySessionService.IsSessionSupported(out var reason);

        return new DoctorCheck
        {
            Name = "display-session",
            Status = supported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            Message = supported ? "Display session is supported." : $"Display session is not supported: {reason}",
            Details = new
            {
                supported,
                reason = string.IsNullOrWhiteSpace(reason) ? null : reason
            }
        };
    }

    private DoctorCheck BuildInputSimulationCheck(bool verbose)
    {
        try
        {
            using var simulator = _inputSimulatorFactory();
            var isSupported = simulator.IsSupported;

            return new DoctorCheck
            {
                Name = "input-simulator",
                Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isSupported
                    ? $"Input simulator backend is available ({simulator.ProviderName})."
                    : $"Input simulator backend is unavailable ({simulator.ProviderName}).",
                Details = verbose
                    ? new
                    {
                        provider = simulator.ProviderName,
                        supported = isSupported
                    }
                    : null
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheck
            {
                Name = "input-simulator",
                Status = DoctorCheckStatus.Fail,
                Message = "Input simulator backend probe failed.",
                Details = verbose ? new { error = ex.Message } : null
            };
        }
    }

    private DoctorCheck BuildInputCaptureCheck(bool verbose)
    {
        try
        {
            using var capture = _inputCaptureFactory();
            var isSupported = capture.IsSupported;

            return new DoctorCheck
            {
                Name = "input-capture",
                Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isSupported
                    ? $"Input capture backend is available ({capture.ProviderName})."
                    : $"Input capture backend is unavailable ({capture.ProviderName}).",
                Details = verbose
                    ? new
                    {
                        provider = capture.ProviderName,
                        supported = isSupported
                    }
                    : null
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheck
            {
                Name = "input-capture",
                Status = DoctorCheckStatus.Fail,
                Message = "Input capture backend probe failed.",
                Details = verbose ? new { error = ex.Message } : null
            };
        }
    }

    private DoctorCheck BuildPositionProviderCheck(bool verbose)
    {
        var isSupported = _mousePositionProvider.IsSupported;

        return new DoctorCheck
        {
            Name = "position-provider",
            Status = isSupported ? DoctorCheckStatus.Pass : DoctorCheckStatus.Warn,
            Message = isSupported
                ? $"Position provider is available ({_mousePositionProvider.ProviderName})."
                : $"Position provider is unavailable ({_mousePositionProvider.ProviderName}); absolute replay may downgrade to fallback mode.",
            Details = verbose
                ? new
                {
                    provider = _mousePositionProvider.ProviderName,
                    supported = isSupported
                }
                : null
        };
    }

    private DoctorCheck BuildMacOSAccessibilityCheck(bool verbose)
    {
        if (!_isMacOS() || _isWindows() || _isLinux())
        {
            return new DoctorCheck
            {
                Name = "macos-accessibility",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS accessibility check was skipped outside macOS.",
                Details = verbose ? new { skipped = true } : null
            };
        }

        if (_permissionChecker is null || !_permissionChecker.IsSupported)
        {
            return new DoctorCheck
            {
                Name = "macos-accessibility",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS accessibility checker is unavailable.",
                Details = verbose ? new { checkerAvailable = false } : null
            };
        }

        try
        {
            var trusted = _permissionChecker.IsAccessibilityTrusted();
            return new DoctorCheck
            {
                Name = "macos-accessibility",
                Status = trusted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = trusted
                    ? "macOS accessibility permission is granted."
                    : "macOS accessibility permission is missing. Grant CrossMacro access in System Settings > Privacy & Security > Accessibility.",
                Details = verbose ? new { trusted } : null
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheck
            {
                Name = "macos-accessibility",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS accessibility permission probe failed.",
                Details = verbose ? new { error = ex.Message } : null
            };
        }
    }

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
        const string uInputPrimary = "/dev/uinput";
        const string uInputAlternate = "/dev/input/uinput";

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
                    uInputPrimary,
                    primaryExists = state.PrimaryUInputExists,
                    canWritePrimary = state.CanWritePrimary,
                    uInputAlternate,
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

        const string uInputPrimary = "/dev/uinput";
        const string uInputAlternate = "/dev/input/uinput";
        var primaryExists = _fileExists(uInputPrimary);
        var alternateExists = _fileExists(uInputAlternate);
        var canWritePrimary = primaryExists && _canOpenForWrite(uInputPrimary);
        var canWriteAlternate = alternateExists && _canOpenForWrite(uInputAlternate);
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

    private static bool CanWriteDirectory(string directory)
    {
        var tempFile = Path.Combine(directory, $".crossmacro-doctor-{Guid.NewGuid():N}.tmp");

        try
        {
            using (File.Create(tempFile))
            {
            }

            File.Delete(tempFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanOpenForWrite(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
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
