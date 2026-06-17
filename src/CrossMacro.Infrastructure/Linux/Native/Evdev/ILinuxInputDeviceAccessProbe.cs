namespace CrossMacro.Infrastructure.Linux.Native.Evdev;

public interface ILinuxInputDeviceAccessProbe
{
    bool HasUsableReadableInputDevices();
}
