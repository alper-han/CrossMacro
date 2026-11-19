using CrossMacro.Platform.Linux.Services.Input;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Creates the <see cref="IInputCapture"/> matching the display server and the capability snapshot.
/// </summary>
public sealed class LinuxCaptureFactory
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly Func<LinuxInputCapture> _directFactory;
    private readonly Func<LinuxIpcInputCapture> _ipcFactory;
    private readonly Func<X11InputCapture> _x11Factory;
    private readonly Func<X11InputCapture, bool> _x11IsSupported;

    internal LinuxCaptureFactory(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<LinuxInputCapture> directFactory,
        Func<LinuxIpcInputCapture> ipcFactory,
        Func<X11InputCapture> x11Factory,
        Func<X11InputCapture, bool>? x11IsSupported = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _directFactory = directFactory ?? throw new ArgumentNullException(nameof(directFactory));
        _ipcFactory = ipcFactory ?? throw new ArgumentNullException(nameof(ipcFactory));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
        _x11IsSupported = x11IsSupported ?? (static x11 => x11.IsSupported);
    }

    /// <summary>Priority: native X11, then daemon or direct device according to the capability snapshot.</summary>
    public IInputCapture Create() =>
        LinuxInputBackendResolver.Resolve<IInputCapture, X11InputCapture>(
            _snapshotProvider.GetSnapshot(),
            _snapshotProvider,
            forCapture: true,
            _x11Factory,
            _x11IsSupported,
            _directFactory,
            _ipcFactory,
            static snapshot => new UnavailableInputCapture(
                LinuxInputBackendResolver.BuildUnavailableMessage(
                    snapshot, "capture backend", "no readable input events were found")));
}
