
namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class LinuxInputDeviceAccessProbe(
    Func<bool> hasUsableReadableInputDevices,
    Func<CancellationToken, ValueTask<bool>>? hasUsableReadableInputDevicesAsync = null) : ILinuxInputDeviceAccessProbe
{
    private readonly Func<bool> _hasUsableReadableInputDevices = hasUsableReadableInputDevices ?? throw new ArgumentNullException(nameof(hasUsableReadableInputDevices));
    private readonly Func<CancellationToken, ValueTask<bool>> _hasUsableReadableInputDevicesAsync = hasUsableReadableInputDevicesAsync ?? (static cancellationToken => HasUsableReadableInputDeviceAccessAsync(cancellationToken));

    public LinuxInputDeviceAccessProbe()
        : this(HasUsableReadableInputDeviceAccess) { /* Empty */ }

    public bool HasUsableReadableInputDevices()
    {
        return _hasUsableReadableInputDevices();
    }

    public async ValueTask<bool> HasUsableReadableInputDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _hasUsableReadableInputDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool HasUsableReadableInputDeviceAccess()
    {
        return InputDeviceHelper.GetAvailableDevices().Count > 0;
    }

    private static async ValueTask<bool> HasUsableReadableInputDeviceAccessAsync(CancellationToken cancellationToken)
    {
        var devices = await InputDeviceHelper.GetAvailableDevicesAsync(cancellationToken).ConfigureAwait(false);
        return devices.Count > 0;
    }

}
