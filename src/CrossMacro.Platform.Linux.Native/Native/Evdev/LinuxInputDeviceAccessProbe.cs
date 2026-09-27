
namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class LinuxInputDeviceAccessProbe(
    Func<bool> hasUsableReadableInputDevices,
    Func<CancellationToken, ValueTask<bool>>? hasUsableReadableInputDevicesAsync = null) : ILinuxInputDeviceAccessProbe
{
    private readonly Func<bool> _hasUsableReadableInputDevices = hasUsableReadableInputDevices ?? throw new ArgumentNullException(nameof(hasUsableReadableInputDevices));
    private readonly Func<CancellationToken, ValueTask<bool>> _hasUsableReadableInputDevicesAsync =
        hasUsableReadableInputDevicesAsync ?? (cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(hasUsableReadableInputDevices());
        });

    public LinuxInputDeviceAccessProbe()
        : this(new InputDeviceDiscovery(new NativeInputDeviceDiscoverySource())) { /* Empty */ }

    internal LinuxInputDeviceAccessProbe(InputDeviceDiscovery discovery)
        : this(
            () => discovery.Scan(logSummary: false, logInaccessibleWarning: false).IsReady,
            async cancellationToken => (await discovery.ScanAsync(logInaccessibleWarning: false, cancellationToken).ConfigureAwait(false)).IsReady)
    { /* Empty */ }

    public bool HasUsableReadableInputDevices()
    {
        return _hasUsableReadableInputDevices();
    }

    public async ValueTask<bool> HasUsableReadableInputDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _hasUsableReadableInputDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

}
