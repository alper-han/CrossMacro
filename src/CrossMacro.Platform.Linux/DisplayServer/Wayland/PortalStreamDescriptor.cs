namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalStreamDescriptor(uint NodeId, IReadOnlyDictionary<string, object> Properties)
{
    public ulong? PipeWireSerial => Properties.TryGetValue("pipewire-serial", out var value) && TryReadUInt64(value, out var serial)
        ? serial
        : null;

    private static bool TryReadUInt64(object? value, out ulong result)
    {
        switch (value)
        {
            case ulong unsigned when unsigned > 0:
                result = unsigned;
                return true;
            case uint unsigned when unsigned > 0:
                result = unsigned;
                return true;
            case long signed when signed > 0:
                result = (ulong)signed;
                return true;
            case string text when ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0:
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
