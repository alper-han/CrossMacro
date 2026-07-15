namespace CrossMacro.Platform.Linux.Native.Evdev;

public interface ILinuxInputDeviceAccessProbe
{
    public bool HasUsableReadableInputDevices();
}
