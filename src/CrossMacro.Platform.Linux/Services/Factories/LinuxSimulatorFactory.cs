
namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IInputSimulator
/// based on the Linux display server and system capabilities.
/// Single Responsibility: Only handles simulator creation logic.
/// </summary>
public class LinuxSimulatorFactory
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
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

        _snapshotProvider = snapshotProvider ?? new LegacyLinuxInputSnapshotAdapter(environmentDetector!, capabilityDetector!);
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
        var snapshot = _snapshotProvider.GetSnapshot();
        return ApplyCompositorInputMapping(CreateFromSnapshot(snapshot), snapshot.Compositor);
    }

    private IInputSimulator CreateFromSnapshot(LinuxCapabilitySnapshot snapshot)
    {
        var x11 = snapshot.IsX11 ? _x11Factory() : null;
        var retainX11 = false;
        try
        {
            var nativeX11Supported = x11 is not null && _x11IsSupported(x11);
            if (!nativeX11Supported && snapshot.IsX11 && _snapshotProvider is LegacyLinuxInputSnapshotAdapter adapter)
            {
                snapshot = adapter.CompleteInputSnapshot(snapshot);
            }
            var selection = LinuxBackendSelectionPolicy.SelectInput(
                snapshot,
                nativeX11Supported,
                forCapture: false);

            if (selection.Backend is LinuxInputBackend.NativeX11)
            {
                retainX11 = true;
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
        finally
        {
            if (!retainX11)
            {
                x11?.Dispose();
            }
        }
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
