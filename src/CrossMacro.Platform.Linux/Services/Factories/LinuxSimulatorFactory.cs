using CrossMacro.Platform.Linux.DisplayServer.Wayland.Cosmic;
using CrossMacro.Platform.Linux.Services.Input;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Creates the <see cref="IInputSimulator"/> matching the display server and the capability snapshot.
/// </summary>
public sealed class LinuxSimulatorFactory
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly Func<LinuxInputSimulator> _directFactory;
    private readonly Func<LinuxIpcInputSimulator> _ipcFactory;
    private readonly Func<X11InputSimulator> _x11Factory;
    private readonly Func<X11InputSimulator, bool> _x11IsSupported;
    private readonly IMousePositionProvider? _positionProvider;

    internal LinuxSimulatorFactory(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<LinuxInputSimulator> directFactory,
        Func<LinuxIpcInputSimulator> ipcFactory,
        Func<X11InputSimulator> x11Factory,
        IMousePositionProvider? positionProvider = null,
        Func<X11InputSimulator, bool>? x11IsSupported = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _directFactory = directFactory ?? throw new ArgumentNullException(nameof(directFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
        _x11IsSupported = x11IsSupported ?? (static x11 => x11.IsSupported);
        _positionProvider = positionProvider;
    }

    /// <summary>Priority: native X11, then daemon or direct device according to the capability snapshot.</summary>
    public IInputSimulator Create()
    {
        var snapshot = _snapshotProvider.GetSnapshot();
        var simulator = LinuxInputBackendResolver.Resolve<IInputSimulator, X11InputSimulator>(
            snapshot,
            _snapshotProvider,
            forCapture: false,
            _x11Factory,
            _x11IsSupported,
            _directFactory,
            _ipcFactory,
            static unavailable => new UnavailableInputSimulator(
                LinuxInputBackendResolver.BuildUnavailableMessage(
                    unavailable, "backend", "direct input fallback is unavailable")));
        return ApplyCompositorInputMapping(simulator, snapshot.Compositor, _positionProvider);
    }

    internal static IInputSimulator ApplyCompositorInputMapping(
        IInputSimulator simulator,
        CompositorType compositor,
        IMousePositionProvider? positionProvider)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        if (compositor is not CompositorType.Cosmic ||
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
