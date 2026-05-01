using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Extensions;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IInputSimulator
/// based on the Linux display server and system capabilities.
/// Single Responsibility: Only handles simulator creation logic.
/// </summary>
public class LinuxSimulatorFactory
{
    private readonly ILinuxEnvironmentDetector _environmentDetector;
    private readonly ILinuxInputCapabilityDetector _capabilityDetector;
    private readonly Func<LinuxInputSimulator> _legacyFactory;
    private readonly Func<LinuxIpcInputSimulator> _ipcFactory;
    private readonly Func<X11InputSimulator> _x11Factory;
    private readonly Func<X11InputSimulator, bool> _x11IsSupported;

    public LinuxSimulatorFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory)
        : this(environmentDetector, capabilityDetector, legacyFactory, ipcFactory, x11Factory, static x11 => x11.IsSupported)
    {
    }

    internal LinuxSimulatorFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory,
        Func<X11InputSimulator, bool> x11IsSupported)
    {
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _legacyFactory = legacyFactory ?? throw new ArgumentNullException(nameof(legacyFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
        _x11IsSupported = x11IsSupported ?? throw new ArgumentNullException(nameof(x11IsSupported));
    }

    /// <summary>
    /// Creates the appropriate input simulator for the current environment.
    /// Priority: Wayland (Daemon or Legacy) -> X11 Native -> Fallback (Legacy or IPC based on capabilities)
    /// </summary>
    public IInputSimulator Create()
    {
        // 1. Wayland -> Check capabilities (Daemon or Legacy fallback)
        if (_environmentDetector.IsWayland)
        {
            var mode = _capabilityDetector.DetermineMode();

            if (mode == InputProviderMode.Daemon)
            {
                LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_Daemon",
                    "[LinuxSimulatorFactory] Wayland detected ({0}), using IPC Simulator (Daemon mode)",
                    _environmentDetector.DetectedCompositor);
                return _ipcFactory();
            }

            if (mode == InputProviderMode.None)
            {
                LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_None",
                    "[LinuxSimulatorFactory] Wayland detected ({0}), no usable input backend found. Returning unsupported simulator.",
                    _environmentDetector.DetectedCompositor);
                return new UnavailableInputSimulator();
            }

            // Fallback to legacy evdev (works with direct device permissions or Flatpak --device=all)
            LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_Legacy",
                "[LinuxSimulatorFactory] Wayland detected ({0}), daemon not available, using Legacy evdev Simulator",
                _environmentDetector.DetectedCompositor);
            return _legacyFactory();
        }

        // 2. X11 -> Try Native X11
        var x11Sim = _x11Factory();
        if (_x11IsSupported(x11Sim))
        {
            LoggingExtensions.LogOnce("LinuxSimulatorFactory_X11", "[LinuxSimulatorFactory] X11 detected, using Native X11 Simulator");
            return x11Sim;
        }

        // 3. Fallback -> Legacy or Daemon based on capabilities
        var fallbackMode = _capabilityDetector.DetermineMode();
        LoggingExtensions.LogOnce("LinuxSimulatorFactory_Fallback", "[LinuxSimulatorFactory] Fallback mode: {0}", fallbackMode);

        return fallbackMode switch
        {
            InputProviderMode.Legacy => _legacyFactory(),
            InputProviderMode.Daemon => _ipcFactory(),
            _ => new UnavailableInputSimulator()
        };
    }

}
