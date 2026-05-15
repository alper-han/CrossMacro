using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Extensions;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IInputCapture
/// based on the Linux display server and system capabilities.
/// Single Responsibility: Only handles capture creation logic.
/// </summary>
public class LinuxCaptureFactory
{
    private readonly ILinuxEnvironmentDetector _environmentDetector;
    private readonly ILinuxInputCapabilityDetector _capabilityDetector;
    private readonly Func<LinuxInputCapture> _legacyFactory;
    private readonly Func<LinuxIpcInputCapture> _ipcFactory;
    private readonly Func<X11InputCapture> _x11Factory;
    private readonly Func<X11InputCapture, bool> _x11IsSupported;

    public LinuxCaptureFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory)
        : this(environmentDetector, capabilityDetector, legacyFactory, ipcFactory, x11Factory, static x11 => x11.IsSupported)
    {
    }

    internal LinuxCaptureFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory,
        Func<X11InputCapture, bool> x11IsSupported)
    {
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _legacyFactory = legacyFactory ?? throw new ArgumentNullException(nameof(legacyFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
        _x11IsSupported = x11IsSupported ?? throw new ArgumentNullException(nameof(x11IsSupported));
    }

    /// <summary>
    /// Creates the appropriate input capture for the current environment.
    /// Priority: Wayland (Daemon or Legacy) -> X11 Native -> Fallback (Legacy or IPC based on capabilities)
    /// </summary>
    public IInputCapture Create()
    {
        // 1. Wayland -> Check capabilities (Daemon or Legacy fallback)
        if (_environmentDetector.IsWayland)
        {
            var mode = _capabilityDetector.DetermineMode();

            if (mode == InputProviderMode.Daemon)
            {
                LoggingExtensions.LogOnce("LinuxCaptureFactory_Wayland_Daemon",
                    "[LinuxCaptureFactory] Wayland detected ({0}), using IPC Capture (Daemon mode)",
                    _environmentDetector.DetectedCompositor);
                return _ipcFactory();
            }

            if (mode == InputProviderMode.None)
            {
                var reason = BuildUnavailableCaptureMessage();
                LoggingExtensions.LogOnce("LinuxCaptureFactory_Wayland_None",
                    "[LinuxCaptureFactory] Wayland detected ({0}), no usable input backend found. Returning unsupported capture: {1}",
                    _environmentDetector.DetectedCompositor,
                    reason);
                return new UnavailableInputCapture(reason);
            }

            if (!_capabilityDetector.CanReadInputEvents)
            {
                var reason = BuildUnavailableCaptureMessage();
                LoggingExtensions.LogOnce("LinuxCaptureFactory_Wayland_Legacy_NoReadableEvents",
                    "[LinuxCaptureFactory] Wayland detected ({0}), direct uinput is available but no readable input events were found. Returning unsupported capture: {1}",
                    _environmentDetector.DetectedCompositor,
                    reason);
                return new UnavailableInputCapture(reason);
            }

            // Fallback to legacy evdev (works with direct device permissions or Flatpak --device=all)
            LoggingExtensions.LogOnce("LinuxCaptureFactory_Wayland_Legacy",
                "[LinuxCaptureFactory] Wayland detected ({0}), daemon not available, using Legacy evdev Capture",
                _environmentDetector.DetectedCompositor);
            return _legacyFactory();
        }

        // 2. X11 -> Try Native X11
        var x11Cap = _x11Factory();
        if (_x11IsSupported(x11Cap))
        {
            LoggingExtensions.LogOnce("LinuxCaptureFactory_X11", "[LinuxCaptureFactory] X11 detected, using Native X11 Capture");
            return x11Cap;
        }

        // 3. Fallback -> Legacy or Daemon based on capabilities
        var fallbackMode = _capabilityDetector.DetermineMode();
        LoggingExtensions.LogOnce("LinuxCaptureFactory_Fallback", "[LinuxCaptureFactory] Fallback mode: {0}", fallbackMode);

        return fallbackMode switch
        {
            InputProviderMode.Legacy when _capabilityDetector.CanReadInputEvents => _legacyFactory(),
            InputProviderMode.Daemon => _ipcFactory(),
            _ => new UnavailableInputCapture(BuildUnavailableCaptureMessage())
        };
    }

    private string BuildUnavailableCaptureMessage()
    {
        var snapshot = _capabilityDetector.GetSnapshot();
        var diagnostic = snapshot.DaemonHandshakeDiagnostic;

        if (diagnostic is not null)
        {
            return diagnostic.Value.Status switch
            {
                LinuxDaemonHandshakeStatus.PermissionDenied => "No usable Linux input capture backend is available: daemon socket permission denied and no readable input events were found.",
                LinuxDaemonHandshakeStatus.MissingSocket => snapshot.CanUseDirectUInput
                    ? "No usable Linux input capture backend is available: daemon socket is missing but direct input fallback is available only without readable input events."
                    : "No usable Linux input capture backend is available: daemon socket is missing and no direct input fallback is available.",
                LinuxDaemonHandshakeStatus.Timeout => snapshot.CanUseDirectUInput
                    ? "No usable Linux input capture backend is available: daemon handshake timed out and direct input fallback is unavailable."
                    : "No usable Linux input capture backend is available: daemon handshake timed out and no direct input fallback is available.",
                _ => snapshot.CanUseDirectUInput && snapshot.CanReadInputEvents
                    ? "No usable Linux input capture backend is available: daemon backend unavailable and direct event access is not usable."
                    : "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found."
            };
        }

        return snapshot.CanReadInputEvents
            ? "No usable Linux input capture backend is available: daemon backend unavailable."
            : "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.";
    }

}
