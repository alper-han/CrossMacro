namespace CrossMacro.Platform.Linux.Native.Evdev;

public interface ILinuxInputDeviceAccessProbe
{
    public bool HasUsableReadableInputDevices();

    public ValueTask<bool> HasUsableReadableInputDevicesAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ValueTask.FromResult(HasUsableReadableInputDevices());
    }
}
