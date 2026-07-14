namespace CrossMacro.Platform.Linux.Native.Evdev;

public interface ILinuxInputDeviceAccessProbe
{
    bool HasUsableReadableInputDevices();
}
