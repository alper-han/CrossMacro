using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Abstractions.Input.Capture;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Permissions;
using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Commands.Diagnostics;

/// <summary>
/// Composes the doctor report from the platform-neutral checks, the macOS permission checks and whatever
/// checks the platform backend contributes through <see cref="IPlatformDoctorChecks"/>.
/// </summary>
public sealed class DoctorService : IDoctorService
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IRuntimeContext _runtimeContext;
    private readonly IDisplaySessionService _displaySessionService;
    private readonly DoctorCapabilityProbeGroup _capabilityProbeGroup;
    private readonly IPermissionChecker? _permissionChecker;
    private readonly string _configDirectory;
    private readonly IMacOSScreenRecordingPermissionProbe? _macOSScreenRecordingPermissionProbe;
    private readonly IPlatformDoctorChecks? _platformChecks;

    public DoctorService(
        IRuntimeContext runtimeContext,
        IEnvironmentInfoProvider environmentInfoProvider,
        IDisplaySessionService displaySessionService,
        Func<IInputSimulator> inputSimulatorFactory,
        Func<IInputCapture> inputCaptureFactory,
        IMousePositionProvider mousePositionProvider,
        ApplicationPaths applicationPaths,
        IPermissionChecker? permissionChecker = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null,
        IPlatformDoctorChecks? platformChecks = null)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _environmentInfoProvider = environmentInfoProvider ?? throw new ArgumentNullException(nameof(environmentInfoProvider));
        _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
        _capabilityProbeGroup = new DoctorCapabilityProbeGroup(inputSimulatorFactory, inputCaptureFactory, mousePositionProvider);
        _configDirectory = applicationPaths.ConfigDirectory;
        _permissionChecker = permissionChecker;
        _macOSScreenRecordingPermissionProbe = macOSScreenRecordingPermissionProbe;
        _platformChecks = platformChecks;
    }

    public async Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var platformWarning = _platformChecks is null
            ? null
            : await _platformChecks.PrepareAsync(verbose, cancellationToken).ConfigureAwait(false);

        var checks = new List<DoctorCheck>
        {
            BuildPlatformCheck(),
            BuildEnvironmentCheck(),
            BuildConfigPathCheck(),
            BuildDisplaySessionCheck(),
        };
        if (platformWarning is not null)
        {
            checks.Add(platformWarning);
        }

        var capabilityChecks = _capabilityProbeGroup.Run(verbose);
        checks.AddRange(capabilityChecks);

        if (_runtimeContext.IsMacOS)
        {
            checks.AddRange(BuildMacOSPermissionChecks(verbose));
        }

        if (_platformChecks is not null)
        {
            checks.AddRange(await _platformChecks.CheckAsync(capabilityChecks, verbose, cancellationToken).ConfigureAwait(false));
        }

        return new DoctorReport
        {
            Checks = checks,
        };
    }

    private static DoctorCheck BuildPlatformCheck()
    {
        var description = RuntimeInformation.OSDescription;
        return new DoctorCheck
        {
            Name = "platform",
            Status = DoctorCheckStatus.Pass,
            Message = $"Platform: {description}",
            Details = new JsonObject
            {
                ["osDescription"] = description,
                ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            },
        };
    }

    private DoctorCheck BuildEnvironmentCheck()
    {
        return new DoctorCheck
        {
            Name = "display-environment",
            Status = DoctorCheckStatus.Pass,
            Message = $"Detected environment: {_environmentInfoProvider.CurrentEnvironment}",
            Details = new JsonObject
            {
                ["currentEnvironment"] = _environmentInfoProvider.CurrentEnvironment.ToString(),
                ["wmHandlesCloseButton"] = _environmentInfoProvider.WindowManagerHandlesCloseButton,
            },
        };
    }

    private DoctorCheck BuildConfigPathCheck()
    {
        var configDirectory = _configDirectory;

        try
        {
            _ = Directory.CreateDirectory(configDirectory);
            var isWritable = CanWriteDirectory(configDirectory);

            return new DoctorCheck
            {
                Name = "config-path",
                Status = isWritable ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
                Message = isWritable
                    ? "Config directory is writable."
                    : "Config directory is not writable.",
                Details = new JsonObject { ["configDirectory"] = configDirectory, ["writable"] = isWritable },
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck
            {
                Name = "config-path",
                Status = DoctorCheckStatus.Fail,
                Message = "Failed to access config directory.",
                Details = new JsonObject { ["configDirectory"] = configDirectory, ["error"] = ex.Message },
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
            Details = new JsonObject
            {
                ["supported"] = supported,
                ["reason"] = string.IsNullOrWhiteSpace(reason) ? null : reason,
            },
        };
    }

    private List<DoctorCheck> BuildMacOSPermissionChecks(bool verbose)
    {
        var checks = new List<DoctorCheck>();

        if (_permissionChecker is null || !_permissionChecker.IsSupported)
        {
            checks.Add(BuildMacOSScreenRecordingCheck(verbose));
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS permission checker is unavailable.",
                Details = verbose ? new JsonObject { ["checkerAvailable"] = false } : null,
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
                Details = verbose ? new JsonObject { ["checkerProvidesSeparateMacOSStatus"] = false } : null,
            });

            checks.Add(new DoctorCheck
            {
                Name = "macos-event-posting",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS event posting status is unavailable from this permission checker.",
                Details = verbose ? new JsonObject { ["checkerProvidesSeparateMacOSStatus"] = false } : null,
            });

            bool trusted;
            try
            {
                trusted = _permissionChecker.IsAccessibilityTrusted();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                checks.Add(new DoctorCheck
                {
                    Name = "macos-accessibility",
                    Status = DoctorCheckStatus.Warn,
                    Message = "macOS Accessibility trust probe failed.",
                    Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            checks.Add(new DoctorCheck
            {
                Name = "macos-input-monitoring",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS permission status probe failed.",
                Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
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
                ? new JsonObject
                {
                    ["listenEventGranted"] = status.ListenEventGranted,
                    ["listenEventApiAvailable"] = status.ListenEventApiAvailable,
                }
                : null,
        });

        checks.Add(new DoctorCheck
        {
            Name = "macos-event-posting",
            Status = status.PostEventGranted ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail,
            Message = status.PostEventGranted
                ? "macOS event posting permission is granted for playback and injection."
                : "macOS event posting permission is missing. Allow event posting for playback and injection; macOS may show this under Accessibility.",
            Details = verbose
                ? new JsonObject
                {
                    ["postEventGranted"] = status.PostEventGranted,
                    ["postEventApiAvailable"] = status.PostEventApiAvailable,
                }
                : null,
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
                Details = verbose ? new JsonObject { ["probeAvailable"] = false } : null,
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
                        ? new JsonObject
                        {
                            ["probeAvailable"] = true,
                            ["preflightApiAvailable"] = false,
                        }
                        : null,
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
                    ? new JsonObject
                    {
                        ["screenRecordingGranted"] = granted,
                        ["preflightApiAvailable"] = true,
                    }
                    : null,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck
            {
                Name = "macos-screen-recording",
                Status = DoctorCheckStatus.Warn,
                Message = "macOS Screen Recording status probe failed.",
                Details = verbose ? new JsonObject { ["error"] = ex.Message } : null,
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
            Details = verbose ? new JsonObject { ["accessibilityTrusted"] = trusted } : null,
        };
    }

    private static bool CanWriteDirectory(string directory)
    {
        var tempFile = Path.Combine(directory, $".crossmacro-doctor-{Guid.NewGuid():N}.tmp");

        try
        {
            using (File.Create(tempFile)) { /* Empty */ }

            File.Delete(tempFile);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
