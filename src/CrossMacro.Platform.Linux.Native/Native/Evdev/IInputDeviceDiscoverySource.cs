namespace CrossMacro.Platform.Linux.Native.Evdev;

internal interface IInputDeviceDiscoverySource
{
    public bool InputDirectoryExists();
    public string[] GetDeviceFiles();
    public string? ReadProcDevices();
    public Task<string?> ReadProcDevicesAsync(CancellationToken cancellationToken);
    public InputDeviceHelper.InputDevice ReadDevice(string path, ProcInputDeviceSnapshot procSnapshot);
    public (bool CanOpen, int Errno) CanOpenForReading(string path);
}
