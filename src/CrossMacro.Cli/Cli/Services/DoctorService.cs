using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;

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
}
