
namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IInputCapture
/// based on the Linux display server and system capabilities.
/// Single Responsibility: Only handles capture creation logic.
/// </summary>
public class LinuxCaptureFactory
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly Func<LinuxInputCapture> _legacyFactory;
    private readonly Func<LinuxIpcInputCapture> _ipcFactory;
    private readonly Func<X11InputCapture> _x11Factory;
    private readonly Func<X11InputCapture, bool> _x11IsSupported;

    internal LinuxCaptureFactory(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory)
        : this(
environmentDetector: null,
capabilityDetector: null,
            snapshotProvider,
            legacyFactory,
            ipcFactory,
            x11Factory,
            static x11 => x11.IsSupported)
    { /* Empty */ }

    public LinuxCaptureFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory)
        : this(environmentDetector, capabilityDetector, snapshotProvider: null, legacyFactory, ipcFactory, x11Factory, static x11 => x11.IsSupported) { /* Empty */ }

    internal LinuxCaptureFactory(
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory,
        Func<X11InputCapture, bool> x11IsSupported)
        : this(environmentDetector, capabilityDetector, snapshotProvider: null, legacyFactory, ipcFactory, x11Factory, x11IsSupported) { /* Empty */ }

    internal LinuxCaptureFactory(
        ILinuxEnvironmentDetector? environmentDetector,
        ILinuxInputCapabilityDetector? capabilityDetector,
        ILinuxCapabilitySnapshotProvider? snapshotProvider,
        Func<LinuxInputCapture> legacyFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory,
        Func<X11InputCapture, bool> x11IsSupported)
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
    }

    /// <summary>
    /// Creates the appropriate input capture for the current environment.
    /// Priority: Wayland (Daemon or Legacy) -> X11 Native -> Fallback (Legacy or IPC based on capabilities)
    /// </summary>
    public IInputCapture Create()
    {
        var snapshot = _snapshotProvider.GetSnapshot();
        return CreateFromSnapshot(snapshot);
    }

    private IInputCapture CreateFromSnapshot(LinuxCapabilitySnapshot snapshot)
    {
        var x11 = snapshot.IsX11 ? _x11Factory() : null;
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
                forCapture: true);

            if (selection.Backend is LinuxInputBackend.NativeX11)
            {
                var selected = x11!;
                x11 = null;
                return selected;
            }

            return selection.Mode switch
            {
                InputProviderMode.Daemon => _ipcFactory(),
                InputProviderMode.Legacy => _legacyFactory(),
                InputProviderMode.None => new UnavailableInputCapture(BuildUnavailableCaptureMessage(snapshot)),
                _ => new UnavailableInputCapture(BuildUnavailableCaptureMessage(snapshot)),
            };
        }
        finally
        {
            x11?.Dispose();
        }
    }

    private static string BuildUnavailableCaptureMessage(LinuxCapabilitySnapshot snapshot) =>
        snapshot.Input.DaemonHandshakeDiagnostic?.Status switch
        {
            LinuxDaemonHandshakeStatus.PermissionDenied => "No usable Linux input capture backend is available: daemon socket permission denied and no readable input events were found.",
            LinuxDaemonHandshakeStatus.Timeout => "No usable Linux input capture backend is available: daemon handshake timed out and no direct input fallback is available.",
            LinuxDaemonHandshakeStatus.Success => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            LinuxDaemonHandshakeStatus.MissingSocket => "No usable Linux input capture backend is available: daemon socket is missing and no direct input fallback is available.",
            LinuxDaemonHandshakeStatus.WrongSocketType => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            LinuxDaemonHandshakeStatus.ProtocolMismatch => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            LinuxDaemonHandshakeStatus.HandshakeRejected => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            LinuxDaemonHandshakeStatus.UnexpectedError => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
            _ when snapshot.Input.CanUseDirectUInput && !snapshot.Input.CanReadInputEvents => "No usable Linux input capture backend is available: direct input events are not readable.",
            _ => "No usable Linux input capture backend is available: daemon backend unavailable and no readable input events were found.",
        };

}
