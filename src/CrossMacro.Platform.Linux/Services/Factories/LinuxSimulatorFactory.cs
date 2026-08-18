
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
    private readonly ILinuxCapabilitySnapshotProvider? _snapshotProvider;
    private readonly Func<LinuxInputSimulator> _legacyFactory;
    private readonly Func<LinuxIpcInputSimulator> _ipcFactory;
    private readonly Func<X11InputSimulator> _x11Factory;
    private readonly Func<X11InputSimulator, bool> _x11IsSupported;
    private readonly IMousePositionProvider? _positionProvider;

    internal LinuxSimulatorFactory(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory,
        IMousePositionProvider? positionProvider = null)
        : this(environmentDetector: null, capabilityDetector: null, snapshotProvider, legacyFactory, ipcFactory, x11Factory, static x11 => x11.IsSupported, positionProvider) { /* Empty */ }

    public LinuxSimulatorFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory)
        : this(environmentDetector, capabilityDetector, snapshotProvider: null, legacyFactory, ipcFactory, x11Factory, static x11 => x11.IsSupported, positionProvider: null) { /* Empty */ }

    internal LinuxSimulatorFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory,
        Func<X11InputSimulator, bool> x11IsSupported,
        IMousePositionProvider? positionProvider = null)
        : this(environmentDetector, capabilityDetector, snapshotProvider: null, legacyFactory, ipcFactory, x11Factory, x11IsSupported, positionProvider) { /* Empty */ }

    internal LinuxSimulatorFactory(
        ILinuxEnvironmentDetector? environmentDetector,
        ILinuxInputCapabilityDetector? capabilityDetector,
        ILinuxCapabilitySnapshotProvider? snapshotProvider,
        Func<LinuxInputSimulator> legacyFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory,
        Func<X11InputSimulator, bool> x11IsSupported,
        IMousePositionProvider? positionProvider)
    {
        if (snapshotProvider is null && environmentDetector is null)
        {
            throw new ArgumentNullException(nameof(environmentDetector));
        }

        if (snapshotProvider is null && capabilityDetector is null)
        {
            throw new ArgumentNullException(nameof(capabilityDetector));
        }

        _environmentDetector = environmentDetector!;
        _capabilityDetector = capabilityDetector!;
        _snapshotProvider = snapshotProvider;
        _legacyFactory = legacyFactory ?? throw new ArgumentNullException(nameof(legacyFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
        _x11IsSupported = x11IsSupported ?? throw new ArgumentNullException(nameof(x11IsSupported));
        _positionProvider = positionProvider;
    }

    /// <summary>
    /// Creates the appropriate input simulator for the current environment.
    /// Priority: Wayland (Daemon or Legacy) -> X11 Native -> Fallback (Legacy or IPC based on capabilities)
    /// </summary>
    public IInputSimulator Create()
    {
        if (_snapshotProvider is not null)
        {
            var snapshot = _snapshotProvider.GetSnapshot();
            return ApplyCompositorInputMapping(CreateFromSnapshot(snapshot), snapshot.Compositor);
        }

        return ApplyCompositorInputMapping(
            CreateFromEnvironment(),
            _environmentDetector.DetectedCompositor);
    }

    private IInputSimulator CreateFromSnapshot(LinuxCapabilitySnapshot snapshot)
    {
        var x11 = snapshot.IsX11 ? _x11Factory() : null;
        var selection = LinuxBackendSelectionPolicy.SelectInput(
            snapshot,
            x11 is not null && _x11IsSupported(x11),
            forCapture: false);

        if (string.Equals(selection.Reason, "native-x11", StringComparison.Ordinal))
        {
            return x11!;
        }

        return selection.Mode switch
        {
            InputProviderMode.Daemon => _ipcFactory(),
            InputProviderMode.Legacy => _legacyFactory(),
            InputProviderMode.None => new UnavailableInputSimulator(BuildUnavailableSimulatorMessage(snapshot)),
            _ => new UnavailableInputSimulator(BuildUnavailableSimulatorMessage(snapshot)),
        };
    }

    private IInputSimulator CreateFromEnvironment()
    {
        if (_environmentDetector.IsWayland)
        {
            return CreateForWaylandEnvironment();
        }

        return CreateForX11OrFallbackEnvironment();
    }

    private IInputSimulator CreateForWaylandEnvironment()
    {
        var mode = _capabilityDetector.DetermineMode();

        if (mode is InputProviderMode.Daemon)
        {
            LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_Daemon",
                "[LinuxSimulatorFactory] Wayland detected ({0}), using IPC Simulator (Daemon mode)",
                _environmentDetector.DetectedCompositor);
            return _ipcFactory();
        }

        if (mode is InputProviderMode.None)
        {
            var reason = BuildUnavailableSimulatorMessage();
            LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_None",
                "[LinuxSimulatorFactory] Wayland detected ({0}), no usable input backend found. Returning unsupported simulator: {1}",
                _environmentDetector.DetectedCompositor,
                reason);
            return new UnavailableInputSimulator(reason);
        }

        LoggingExtensions.LogOnce("LinuxSimulatorFactory_Wayland_Legacy",
            "[LinuxSimulatorFactory] Wayland detected ({0}), daemon not available, using Legacy evdev Simulator",
            _environmentDetector.DetectedCompositor);
        return _legacyFactory();
    }

    private IInputSimulator CreateForX11OrFallbackEnvironment()
    {
        var x11Sim = _x11Factory();
        if (_x11IsSupported(x11Sim))
        {
            LoggingExtensions.LogOnce("LinuxSimulatorFactory_X11", "[LinuxSimulatorFactory] X11 detected, using Native X11 Simulator");
            return x11Sim;
        }

        var fallbackMode = _capabilityDetector.DetermineMode();
        LoggingExtensions.LogOnce("LinuxSimulatorFactory_Fallback", "[LinuxSimulatorFactory] Fallback mode: {0}", fallbackMode);

        return fallbackMode switch
        {
            InputProviderMode.Legacy => _legacyFactory(),
            InputProviderMode.Daemon => _ipcFactory(),
            InputProviderMode.None => new UnavailableInputSimulator(BuildUnavailableSimulatorMessage()),
            _ => new UnavailableInputSimulator(BuildUnavailableSimulatorMessage()),
        };
    }

    private string BuildUnavailableSimulatorMessage()
    {
        var snapshot = _capabilityDetector.GetSnapshot();
        var diagnostic = snapshot.DaemonHandshakeDiagnostic;

        if (diagnostic is not null)
        {
            return diagnostic.Value.Status switch
            {
                LinuxDaemonHandshakeStatus.PermissionDenied => "No usable Linux input backend is available: daemon socket permission denied and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.MissingSocket => "No usable Linux input backend is available: daemon socket is missing and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.Timeout => "No usable Linux input backend is available: daemon handshake timed out and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.Success => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.WrongSocketType => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.ProtocolMismatch => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.HandshakeRejected => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                LinuxDaemonHandshakeStatus.UnexpectedError => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
                _ => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            };
        }

        return "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.";
    }

    private static string BuildUnavailableSimulatorMessage(LinuxCapabilitySnapshot snapshot) =>
        snapshot.Input.DaemonHandshakeDiagnostic?.Status switch
        {
            LinuxDaemonHandshakeStatus.PermissionDenied => "No usable Linux input backend is available: daemon socket permission denied and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.Timeout => "No usable Linux input backend is available: daemon handshake timed out and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.Success => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.MissingSocket => "No usable Linux input backend is available: daemon socket is missing and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.WrongSocketType => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.ProtocolMismatch => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.HandshakeRejected => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            LinuxDaemonHandshakeStatus.UnexpectedError => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
            _ => "No usable Linux input backend is available: daemon backend unavailable and direct input fallback is unavailable.",
        };

    private IInputSimulator ApplyCompositorInputMapping(
        IInputSimulator simulator,
        CompositorType compositor)
    {
        return ApplyCompositorInputMapping(simulator, compositor, _positionProvider);
    }

    internal static IInputSimulator ApplyCompositorInputMapping(
        IInputSimulator simulator,
        CompositorType compositor,
        IMousePositionProvider? positionProvider)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        if (compositor is not CompositorType.COSMIC ||
            simulator is UnavailableInputSimulator ||
            positionProvider is not IOutputTopologyProvider topologyProvider)
        {
            return simulator;
        }

        return new CosmicAbsoluteInputSimulator(
            simulator,
            positionProvider,
            topologyProvider);
    }

}
