namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>One proc discovery snapshot, indexed by complete event token and device name.</summary>
internal sealed class ProcInputDeviceSnapshot
{
    private const string NamePrefix = "N: Name=";
    private const string HandlersPrefix = "H: Handlers=";
    private readonly Dictionary<(string EventName, string DeviceName), InputDeviceHandlers> _devices = [];

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
        List<string> eventNames = [];
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                snapshot.AddDevice(name, handlers, eventNames);
                name = null;
                handlers = InputDeviceHandlers.None;
                eventNames.Clear();
            }
            else if (line.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                var value = line.AsSpan(NamePrefix.Length).Trim();
                name = value.Length >= 2 && value[0] is '"' && value[^1] is '"' ? value[1..^1].ToString() : null;
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

        snapshot.AddDevice(name, handlers, eventNames);
        return snapshot;
    }

    internal bool HasHandler(string devicePath, string deviceName, InputDeviceHandlers handler)
        => handler is not InputDeviceHandlers.None
           && _devices.TryGetValue((Path.GetFileName(devicePath), deviceName), out var handlers)
           && handlers.HasFlag(handler);

    private void AddDevice(string? name, InputDeviceHandlers handlers, List<string> eventNames)
    {
        if (name is null)
        {
            return;
        }

        foreach (var eventName in eventNames)
        {
            var key = (eventName, name);
            _ = _devices.TryGetValue(key, out var existing);
            _devices[key] = existing | handlers;
        }
    }

    private static bool IsNumberedToken(string token, string prefix)
        => token.StartsWith(prefix, StringComparison.Ordinal)
           && token.Length > prefix.Length
           && token.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;
}
