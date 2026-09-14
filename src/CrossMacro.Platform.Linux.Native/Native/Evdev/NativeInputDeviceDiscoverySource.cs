namespace CrossMacro.Platform.Linux.Native.Evdev;

internal sealed class NativeInputDeviceDiscoverySource : IInputDeviceDiscoverySource
{
    internal const string InputDirectory = "/dev/input";
    private const string ProcDevicesPath = "/proc/bus/input/devices";

    public bool InputDirectoryExists() => Directory.Exists(InputDirectory);
    public string[] GetDeviceFiles() => Directory.GetFiles(InputDirectory, "event*");
    public string? ReadProcDevices() => File.Exists(ProcDevicesPath) ? File.ReadAllText(ProcDevicesPath) : null;

    public async Task<string?> ReadProcDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return File.Exists(ProcDevicesPath)
            ? await File.ReadAllTextAsync(ProcDevicesPath, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public InputDeviceHelper.InputDevice ReadDevice(string path, ProcInputDeviceSnapshot procSnapshot)
        => InputDeviceHelper.GetDeviceInfo(path, procSnapshot);

    public (bool CanOpen, int Errno) CanOpenForReading(string path) => InputDeviceHelper.CanOpenForReading(path);
}
