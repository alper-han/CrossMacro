namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>One proc discovery snapshot, indexed by complete event token.</summary>
internal sealed class ProcInputDeviceSnapshot
{
    private const string NamePrefix = "N: Name=";
    private const string HandlersPrefix = "H: Handlers=";
    private readonly Dictionary<string, DeviceMetadata> _devices = new(StringComparer.Ordinal);

    private readonly record struct DeviceMetadata(string Name, InputDeviceHandlers Handlers, bool IsExcluded);

    private ProcInputDeviceSnapshot() { }

    internal static ProcInputDeviceSnapshot Parse(string? content)
    {
        var snapshot = new ProcInputDeviceSnapshot();
        if (string.IsNullOrEmpty(content))
        {
            return snapshot;
        }

        using var reader = new StringReader(content);
        string? name = null;
        var handlers = InputDeviceHandlers.None;
        ushort vendorId = 0;
        ushort productId = 0;
        List<string> eventNames = [];
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                snapshot.AddDevice(name, handlers, eventNames, vendorId, productId);
                name = null;
                handlers = InputDeviceHandlers.None;
                eventNames.Clear();
                vendorId = productId = 0;
            }
            else if (line.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                var value = line.AsSpan(NamePrefix.Length).Trim();
                name = value.Length >= 2 && value[0] is '"' && value[^1] is '"' ? value[1..^1].ToString() : null;
            }
            else if (line.StartsWith("I: ", StringComparison.Ordinal))
            {
                foreach (var token in line.AsSpan(3).Split((ReadOnlySpan<char>)" "))
                {
                    var field = line.AsSpan(3)[token];
                    if (field.StartsWith("Vendor=", StringComparison.Ordinal))
                    {
                        _ = ushort.TryParse(field[7..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vendorId);
                    }
                    else if (field.StartsWith("Product=", StringComparison.Ordinal))
                    {
                        _ = ushort.TryParse(field[8..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out productId);
                    }
                }
            }
            else if (line.StartsWith(HandlersPrefix, StringComparison.Ordinal))
            {
                foreach (var token in line[HandlersPrefix.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IsNumberedToken(token, "event"))
                    {
                        eventNames.Add(token);
                    }
                    else if (IsNumberedToken(token, "mouse"))
                    {
                        handlers |= InputDeviceHandlers.Mouse;
                    }
                    else if (string.Equals(token, "kbd", StringComparison.Ordinal))
                    {
                        handlers |= InputDeviceHandlers.Keyboard;
                    }
                }
            }
        }

        snapshot.AddDevice(name, handlers, eventNames, vendorId, productId);
        return snapshot;
    }

    internal bool HasHandler(string devicePath, string deviceName, InputDeviceHandlers handler)
        => handler is not InputDeviceHandlers.None
           && _devices.TryGetValue(Path.GetFileName(devicePath), out var device)
           && string.Equals(device.Name, deviceName, StringComparison.Ordinal)
           && device.Handlers.HasFlag(handler);

    // Proc handlers prove relevance, not irrelevance: ioctl may identify devices
    // without kbd/mouse handlers. Only the shared exclusions can rule them out.
    internal bool? IsRelevantDevice(string devicePath)
    {
        if (!_devices.TryGetValue(Path.GetFileName(devicePath), out var device))
        {
            return null;
        }
        if (device.IsExcluded)
        {
            return false;
        }
        return device.Handlers is not InputDeviceHandlers.None ? true : null;
    }

    private void AddDevice(string? name, InputDeviceHandlers handlers, List<string> eventNames, ushort vendorId, ushort productId)
    {
        if (name is null)
        {
            return;
        }

        var isExcluded = InputDeviceClassification.ShouldExclude(name) ||
                         VirtualDeviceConstants.IsCrossMacroVirtualDevice(name, vendorId, productId);
        foreach (var eventName in eventNames)
        {
            if (_devices.TryGetValue(eventName, out var existing) && string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                handlers |= existing.Handlers;
            }
            _devices[eventName] = new(name, handlers, isExcluded);
        }
    }

    private static bool IsNumberedToken(string token, string prefix)
        => token.StartsWith(prefix, StringComparison.Ordinal)
           && token.Length > prefix.Length
           && token.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;
}
