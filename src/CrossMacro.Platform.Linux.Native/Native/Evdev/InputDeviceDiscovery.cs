using InputDevice = CrossMacro.Platform.Linux.Native.Evdev.InputDeviceHelper.InputDevice;

namespace CrossMacro.Platform.Linux.Native.Evdev;

internal sealed class InputDeviceDiscovery(IInputDeviceDiscoverySource source)
{
    private readonly IInputDeviceDiscoverySource _source = source ?? throw new ArgumentNullException(nameof(source));

    internal IReadOnlyList<InputDevice> GetAvailableDevices(bool logSummary, bool logInaccessibleWarning)
    {
        var files = FindDeviceFiles(logSummary);
        return files is null ? [] : ScanDeviceFiles(files, ReadProcDevicesContent(), logSummary, logInaccessibleWarning, CancellationToken.None);
    }

    internal async Task<IReadOnlyList<InputDevice>> GetAvailableDevicesAsync(bool logInaccessibleWarning, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = FindDeviceFiles(logSummary: true);
        if (files is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        var content = await ReadProcDevicesContentAsync(cancellationToken).ConfigureAwait(false);
        return ScanDeviceFiles(files, content, logSummary: true, logInaccessibleWarning, cancellationToken);
    }

    private string[]? FindDeviceFiles(bool logSummary)
    {
        if (logSummary)
        {
            Log.Information("[InputDeviceHelper] Scanning input devices in {InputDir}...", NativeInputDeviceDiscoverySource.InputDirectory);
        }

        if (!_source.InputDirectoryExists())
        {
            Log.Warning("[InputDeviceHelper] Directory {InputDir} does not exist.", NativeInputDeviceDiscoverySource.InputDirectory);
            return null;
        }

        var files = _source.GetDeviceFiles();
        Log.Debug("[InputDeviceHelper] Found {Count} event files to analyze.", files.Length);
        return files;
    }

    private string? ReadProcDevicesContent()
    {
        try
        {
            return _source.ReadProcDevices();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
            return null;
        }
    }

    private async Task<string?> ReadProcDevicesContentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await _source.ReadProcDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
            return null;
        }
    }

    private IReadOnlyList<InputDevice> ScanDeviceFiles(string[] files, string? procContent, bool logSummary, bool logInaccessibleWarning, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var procSnapshot = ProcInputDeviceSnapshot.Parse(procContent);
        List<InputDevice> devices = [];
        List<InputDevice> skippedDevices = [];
        List<(InputDevice device, int errno)> inaccessibleDevices = [];
        var readErrors = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var device = _source.ReadDevice(file, procSnapshot);
                cancellationToken.ThrowIfCancellationRequested();
                if (device.IsMouse || device.IsKeyboard)
                {
                    var (canOpen, errno) = _source.CanOpenForReading(file);
                    if (canOpen)
                    {
                        devices.Add(device);
                    }
                    else
                    {
                        inaccessibleDevices.Add((device, errno));
                    }
                }
                else
                {
                    skippedDevices.Add(device);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InputDeviceHelper.DeviceOpenException ex) when (ex.Errno is EvdevErrorCodes.AccessDenied or EvdevErrorCodes.Busy)
            {
                inaccessibleDevices.Add((InputDeviceHelper.CreateInaccessiblePlaceholder(file), ex.Errno));
            }
            catch (InputDeviceHelper.DeviceOpenException ex) when (ex.Errno is EvdevErrorCodes.NotFound)
            {
                Log.Debug("[InputDeviceHelper] Device file {File} disappeared before it could be opened (race condition).", file);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                readErrors++;
                Log.LogError(ex, "[InputDeviceHelper] Error reading {File}", file);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (logSummary)
        {
            LogDeviceSummary(files.Length, devices, inaccessibleDevices, skippedDevices, readErrors, logInaccessibleWarning);
        }
        return devices;
    }

    private static void LogDeviceSummary(
        int fileCount,
        List<InputDevice> devices,
        List<(InputDevice device, int errno)> inaccessibleDevices,
        List<InputDevice> skippedDevices,
        int readErrors,
        bool logInaccessibleWarning)
    {
        Log.Information("[InputDeviceHelper] ========== Device Summary ==========");
        Log.Information("[InputDeviceHelper] Total: {Total} | Usable: {Usable} | Inaccessible: {Inaccessible} | Skipped: {Skipped} | ReadErrors: {ReadErrors}",
            fileCount, devices.Count, inaccessibleDevices.Count, skippedDevices.Count, readErrors);

        if (devices.Count > 0)
        {
            Log.Information("[InputDeviceHelper] --- Active Input Devices ---");
            foreach (var dev in devices)
            {
                Log.Information("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | Bus: {Bus} | VID:0x{VID:X4} PID:0x{PID:X4}",
                    dev.DeviceType, dev.Name, dev.Path, InputDeviceClassification.GetBusTypeName(dev.BusType), dev.VendorId, dev.ProductId);
            }
        }

        LogInaccessibleDevices(inaccessibleDevices, logInaccessibleWarning);
        LogSkippedDevices(skippedDevices);
        Log.Information("[InputDeviceHelper] ====================================");
    }

    private static void LogInaccessibleDevices(
        List<(InputDevice device, int errno)> inaccessibleDevices,
        bool logWarning)
    {
        if (inaccessibleDevices.Count is 0)
        {
            return;
        }

        if (logWarning)
        {
            Log.Warning(
                "[InputDeviceHelper] {Count} input device(s) are inaccessible; direct evdev access may be unavailable. Detailed device entries are available at Debug level.",
                inaccessibleDevices.Count);
        }
        foreach (var (dev, errno) in inaccessibleDevices)
        {
            if (errno is EvdevErrorCodes.Busy)
            {
                Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) - Device is exclusively grabbed. Run: sudo fuser -v {Path}",
                    dev.DeviceType, dev.Name, dev.Path, dev.Path);
            }
            else
            {
                Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4} - Cannot open (errno: {Errno})",
                    dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId, errno);
            }
        }
    }

    private static void LogSkippedDevices(List<InputDevice> skippedDevices)
    {
        if (skippedDevices.Count is 0)
        {
            return;
        }

        Log.Debug("[InputDeviceHelper] --- Skipped Devices (not input devices) ---");
        foreach (var dev in skippedDevices)
        {
            Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4}",
                dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId);
        }
    }

}
