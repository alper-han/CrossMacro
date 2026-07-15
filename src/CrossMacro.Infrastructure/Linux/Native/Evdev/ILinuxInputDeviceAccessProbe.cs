namespace CrossMacro.Infrastructure.Linux.Native.Evdev;

public interface ILinuxInputDeviceAccessProbe
{
    public bool HasUsableReadableInputDevices();
}
