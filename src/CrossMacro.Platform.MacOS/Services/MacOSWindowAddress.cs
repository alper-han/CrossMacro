namespace CrossMacro.Platform.MacOS.Services;

internal readonly record struct MacOSWindowAddress(
    int Pid,
    uint WindowId,
    string Title,
    int X,
    int Y,
    int Width,
    int Height)
{
    private const string Prefix = "ax2-";
    private const int MaximumAddressLength = 32_768;

    public static MacOSWindowAddress FromWindow(int pid, uint windowId, string title, ScreenRect frame) =>
        new(pid, windowId, title, frame.X, frame.Y, frame.Width, frame.Height);

    public string Format()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Pid);
            writer.Write(WindowId);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(Title);
        }

        return Prefix + Convert.ToBase64String(stream.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryParse(string address, out MacOSWindowAddress result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(address)
            || address.Length > MaximumAddressLength
            || !address.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var payload = address[Prefix.Length..];
            var suffixIndex = payload.IndexOf('.', StringComparison.Ordinal);
            if (suffixIndex >= 0)
            {
                payload = payload[..suffixIndex];
            }

            var base64 = payload.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var pid = reader.ReadInt32();
            var windowId = reader.ReadUInt32();
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var title = reader.ReadString();
            if (stream.Position != stream.Length || pid <= 0 || width <= 0 || height <= 0)
            {
                return false;
            }

            result = new MacOSWindowAddress(pid, windowId, title, x, y, width, height);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or EndOfStreamException or IOException)
        {
            return false;
        }
    }
}
