using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.Infrastructure.Linux.Native.Evdev;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Cli.Services;

public sealed partial class DoctorService : IDoctorService
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly IMousePositionProvider _mousePositionProvider;
    private readonly IPermissionChecker? _permissionChecker;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string?> _readAllTextIfExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly ILinuxInputDeviceAccessProbe _inputDeviceAccessProbe;
    private readonly Func<bool> _isLinux;
    private readonly Func<bool> _isWindows;
    private readonly Func<bool> _isMacOS;
    private readonly Func<string, bool> _daemonHandshakeProbe;
    private readonly Func<string, LinuxDaemonSocketAccessResult> _daemonSocketAccessProbe;
    private readonly Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult> _daemonHandshakeDiagnosticProbe;
    private readonly IScreenReadingDiagnosticProvider? _screenReadingDiagnosticProvider;
    private readonly IMacOSScreenRecordingPermissionProbe? _macOSScreenRecordingPermissionProbe;

    public DoctorService(
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        IPermissionChecker? permissionChecker = null,
        Func<string, bool>? daemonHandshakeProbe = null,
        Func<string, LinuxDaemonSocketAccessResult>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        IScreenReadingDiagnosticProvider? screenReadingDiagnosticProvider = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null)
        : this(
            environmentInfoProvider,
            displaySessionService,
            Environment.GetEnvironmentVariable,
            File.Exists,
            CanOpenForWrite,
            CanOpenForRead,
            GetInputEventCandidates,
            inputSimulatorFactory,
            inputCaptureFactory,
            mousePositionProvider,
            permissionChecker,
            OperatingSystem.IsLinux,
            OperatingSystem.IsWindows,
            OperatingSystem.IsMacOS,
            daemonHandshakeProbe,
            daemonSocketAccessProbe,
            daemonHandshakeDiagnosticProbe,
            ReadAllTextIfExists,
            null,
            screenReadingDiagnosticProvider,
            macOSScreenRecordingPermissionProbe)
    {
    }

    public DoctorService(
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool>? canOpenForRead,
        Func<string[]>? getInputEventCandidates,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        IPermissionChecker? permissionChecker = null,
        Func<bool>? isLinux = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        Func<string, bool>? daemonHandshakeProbe = null,
        Func<string, LinuxDaemonSocketAccessResult>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        Func<string, string?>? readAllTextIfExists = null,
        Func<bool>? hasUsableReadableInputDevices = null,
        IScreenReadingDiagnosticProvider? screenReadingDiagnosticProvider = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null)
        : this(
            environmentInfoProvider,
            displaySessionService,
            getEnvironmentVariable,
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(hasUsableReadableInputDevices ?? (() => HasReadableInputEventAccess(canOpenForRead ?? CanOpenForRead, getInputEventCandidates ?? GetInputEventCandidates))),
            getInputEventCandidates ?? GetInputEventCandidates,
            inputSimulatorFactory,
            inputCaptureFactory,
            mousePositionProvider,
            permissionChecker,
            isLinux,
            isWindows,
            isMacOS,
            daemonHandshakeProbe,
            daemonSocketAccessProbe,
            daemonHandshakeDiagnosticProbe,
            readAllTextIfExists,
            screenReadingDiagnosticProvider,
            macOSScreenRecordingPermissionProbe)
    {
    }

    internal DoctorService(
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        ILinuxInputDeviceAccessProbe inputDeviceAccessProbe,
        Func<string[]>? getInputEventCandidates,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        IPermissionChecker? permissionChecker = null,
        Func<bool>? isLinux = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        Func<string, bool>? daemonHandshakeProbe = null,
        Func<string, LinuxDaemonSocketAccessResult>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        Func<string, string?>? readAllTextIfExists = null,
        IScreenReadingDiagnosticProvider? screenReadingDiagnosticProvider = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null)
    {
        _environmentInfoProvider = environmentInfoProvider ?? throw new ArgumentNullException(nameof(environmentInfoProvider));
        _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readAllTextIfExists = readAllTextIfExists ?? ReadAllTextIfExists;
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputDeviceAccessProbe = inputDeviceAccessProbe ?? throw new ArgumentNullException(nameof(inputDeviceAccessProbe));
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);
        _inputSimulatorFactory = inputSimulatorFactory ?? throw new ArgumentNullException(nameof(inputSimulatorFactory));
        _inputCaptureFactory = inputCaptureFactory ?? throw new ArgumentNullException(nameof(inputCaptureFactory));
        _mousePositionProvider = mousePositionProvider ?? throw new ArgumentNullException(nameof(mousePositionProvider));
        _permissionChecker = permissionChecker;
        _isLinux = isLinux ?? OperatingSystem.IsLinux;
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
        _daemonHandshakeProbe = daemonHandshakeProbe ?? ProbeDaemonHandshake;
        _daemonSocketAccessProbe = daemonSocketAccessProbe ?? ProbeDaemonSocketAccess;
        _daemonHandshakeDiagnosticProbe = daemonHandshakeDiagnosticProbe ?? ProbeDaemonHandshakeDiagnostic;
        _screenReadingDiagnosticProvider = screenReadingDiagnosticProvider;
        _macOSScreenRecordingPermissionProbe = macOSScreenRecordingPermissionProbe;
    }

    private static bool HasReadableInputEventAccess(Func<string, bool> canOpenForRead, Func<string[]> getInputEventCandidates)
    {
        var eventDevices = getInputEventCandidates();
        return eventDevices.Length > 0 && eventDevices.Any(canOpenForRead);
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
            checks.AddRange(BuildMacOSPermissionChecks(verbose));
        }

        if (_isLinux())
        {
            var linuxState = BuildLinuxInputState();
            checks.Add(BuildLinuxDisplayVariablesCheck(verbose));
            checks.Add(BuildLinuxDaemonSocketCheck(linuxState, verbose));
            checks.Add(BuildLinuxDaemonAccessCheck(linuxState, verbose));
            checks.Add(BuildLinuxDaemonGroupCheck(linuxState, verbose));
            checks.Add(BuildLinuxDaemonHandshakeCheck(linuxState, verbose));
            checks.Add(BuildLinuxUInputCheck(linuxState, verbose));
            checks.Add(BuildLinuxInputReadinessCheck(linuxState, verbose));
            checks.Add(BuildLinuxGsrCompatibilityCheck(linuxState, verbose));
            if (_screenReadingDiagnosticProvider is not null)
            {
                checks.Add(BuildLinuxScreenReadingCheck(verbose));
            }
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
        var configDirectory = PathHelper.GetConfigDirectory();

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

    private IEnumerable<DoctorCheck> BuildMacOSPermissionChecks(bool verbose)
    {
        var checks = new List<DoctorCheck>();

        if (!_isMacOS() || _isWindows() || _isLinux())
        {
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS Input Monitoring check was skipped outside macOS.",
                Details = verbose ? new { skipped = true } : null
            });

            return checks;
        }

        if (_permissionChecker is null || !_permissionChecker.IsSupported)
        {
            checks.Add(BuildMacOSScreenRecordingCheck(verbose));
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS permission checker is unavailable.",
                Details = verbose ? new { checkerAvailable = false } : null
            });

            return checks;
        }

        if (_permissionChecker is not IMacOSPermissionChecker macOSPermissionChecker)
        {
            checks.Add(BuildMacOSScreenRecordingCheck(verbose));
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS Input Monitoring status is unavailable from this permission checker.",
                Details = verbose ? new { checkerProvidesSeparateMacOSStatus = false } : null
            });

            checks.Add(new DoctorCheck
            {
                Name = "macos-event-posting",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS event posting status is unavailable from this permission checker.",
                Details = verbose ? new { checkerProvidesSeparateMacOSStatus = false } : null
            });

            bool trusted;
            try
            {
                trusted = _permissionChecker.IsAccessibilityTrusted();
            }
            catch (Exception ex)
            {
                checks.Add(new DoctorCheck
                {
                    Name = "macos-accessibility",
                    Status = DoctorCheckStatus.Warn,
                    Message = "macOS Accessibility trust probe failed.",
                    Details = verbose ? new { error = ex.Message } : null
                });

                return checks;
            }

            checks.Add(BuildMacOSAccessibilityCheck(trusted, verbose));
            return checks;
        }

        checks.Add(BuildMacOSScreenRecordingCheck(verbose));

        MacOSPermissionStatus status;
        try
        {
            status = macOSPermissionChecker.GetCurrentStatus();
        }
        catch (Exception ex)
        {
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS permission status probe failed.",
                Details = verbose ? new { error = ex.Message } : null
            });

            return checks;
        }

        checks.Add(new DoctorCheck
        {
            Name = "macos-input-monitoring",
            Status = status.ListenEventGranted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            Message = status.ListenEventGranted
                ? "macOS Input Monitoring permission is granted for capture and recording."
                : "macOS Input Monitoring permission is missing. Grant CrossMacro access in System Settings > Privacy & Security > Input Monitoring for capture and recording.",
            Details = verbose
                ? new
                {
                    listenEventGranted = status.ListenEventGranted,
                    listenEventApiAvailable = status.ListenEventApiAvailable
                }
                : null
        });

        checks.Add(new DoctorCheck
        {
            Name = "macos-event-posting",
            Status = status.PostEventGranted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            Message = status.PostEventGranted
                ? "macOS event posting permission is granted for playback and injection."
                : "macOS event posting permission is missing. Allow event posting for playback and injection; macOS may show this under Accessibility.",
            Details = verbose
                ? new
                {
                    postEventGranted = status.PostEventGranted,
                    postEventApiAvailable = status.PostEventApiAvailable
                }
                : null
        });

        checks.Add(BuildMacOSAccessibilityCheck(status.AccessibilityGranted, verbose));
        return checks;
    }

    private DoctorCheck BuildMacOSScreenRecordingCheck(bool verbose)
    {
        if (_macOSScreenRecordingPermissionProbe is null)
        {
            return new DoctorCheck
            {
                Name = "macos-screen-recording",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS Screen Recording status is unavailable from this platform backend.",
                Details = verbose ? new { probeAvailable = false } : null
            };
        }

        try
        {
            var preflightAvailable = _macOSScreenRecordingPermissionProbe.IsPreflightAvailable;
            if (!preflightAvailable)
            {
                return new DoctorCheck
                {
                    Name = "macos-screen-recording",
                    Status = DoctorCheckStatus.Warn,
                    Message = "macOS Screen Recording preflight API is unavailable; screen-reading permission status cannot be checked.",
                    Details = verbose
                        ? new
                        {
                            probeAvailable = true,
                            preflightApiAvailable = false
                        }
                        : null
                };
            }

            var granted = _macOSScreenRecordingPermissionProbe.IsGranted();
            return new DoctorCheck
            {
                Name = "macos-screen-recording",
                Status = granted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = granted
                    ? "macOS Screen Recording permission is granted for screen reading."
                    : "macOS Screen Recording permission is missing. Grant CrossMacro access in System Settings > Privacy & Security > Screen Recording, then restart CrossMacro.",
                Details = verbose
                    ? new
                    {
                        screenRecordingGranted = granted,
                        preflightApiAvailable = true
                    }
                    : null
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheck
            {
                Name = "macos-screen-recording",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS Screen Recording status probe failed.",
                Details = verbose ? new { error = ex.Message } : null
            };
        }
    }

    private static DoctorCheck BuildMacOSAccessibilityCheck(bool trusted, bool verbose)
    {
        return new DoctorCheck
        {
            Name = "macos-accessibility",
            Status = trusted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            Message = trusted
                ? "macOS Accessibility trust is granted for AX features."
                : "macOS Accessibility trust is missing for AX features. Grant CrossMacro access in System Settings > Privacy & Security > Accessibility only if AX features are needed.",
            Details = verbose ? new { accessibilityTrusted = trusted } : null
        };
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

    private static bool CanOpenForRead(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string[] GetInputEventCandidates()
    {
        try
        {
            return Directory.Exists("/dev/input")
                ? Directory.GetFiles("/dev/input", "event*")
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static string? ReadAllTextIfExists(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
