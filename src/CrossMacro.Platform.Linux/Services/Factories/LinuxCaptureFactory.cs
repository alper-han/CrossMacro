using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Extensions;
using Serilog;

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

    public LinuxCaptureFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory)
    {
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _legacyFactory = legacyFactory ?? throw new ArgumentNullException(nameof(legacyFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
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

            // Fallback to legacy evdev (works with input group or Flatpak --device=all)
            LoggingExtensions.LogOnce("LinuxCaptureFactory_Wayland_Legacy",
                "[LinuxCaptureFactory] Wayland detected ({0}), daemon not available, using Legacy evdev Capture",
                _environmentDetector.DetectedCompositor);
            return _legacyFactory();
        }

        // 2. X11 -> Try Native X11
        var x11Cap = _x11Factory();
        if (x11Cap.IsSupported)
        {
            LoggingExtensions.LogOnce("LinuxCaptureFactory_X11", "[LinuxCaptureFactory] X11 detected, using Native X11 Capture");
            return x11Cap;
        }

        // 3. Fallback -> Legacy or Daemon based on capabilities
        var fallbackMode = _capabilityDetector.DetermineMode();
        LoggingExtensions.LogOnce("LinuxCaptureFactory_Fallback", "[LinuxCaptureFactory] Fallback mode: {0}", fallbackMode);

        return fallbackMode == InputProviderMode.Legacy
            ? _legacyFactory()
            : _ipcFactory();
    }

}
