using System;

namespace CrossMacro.Infrastructure.Linux.Native.Evdev;

public sealed class LinuxInputDeviceAccessProbe : ILinuxInputDeviceAccessProbe
{
    private readonly Func<bool> _hasUsableReadableInputDevices;

    public LinuxInputDeviceAccessProbe()
        : this(HasUsableReadableInputDeviceAccess)
    {
    }

    public LinuxInputDeviceAccessProbe(Func<bool> hasUsableReadableInputDevices)
    {
        _hasUsableReadableInputDevices = hasUsableReadableInputDevices ?? throw new ArgumentNullException(nameof(hasUsableReadableInputDevices));
    }

    public bool HasUsableReadableInputDevices()
    {
        return _hasUsableReadableInputDevices();
    }

    private static bool HasUsableReadableInputDeviceAccess()
    {
        return InputDeviceHelper.GetAvailableDevices().Count > 0;
    }

}
