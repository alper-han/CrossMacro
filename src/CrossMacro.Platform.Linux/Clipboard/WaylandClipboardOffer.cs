namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardOffer(
    WaylandClipboardConnection connection,
    IntPtr proxy,
    WaylandClipboardMode mode) : WaylandProxyListener
{
    private const uint DestructorFlag = 1;
    private static readonly string[] PreferredTextMimeTypes = ["text/plain;charset=utf-8", "text/plain", "UTF8_STRING", "TEXT", "STRING"];

    private readonly WaylandClipboardConnection _connection = connection;
    private readonly WaylandClipboardMode _mode = mode;
    private readonly HashSet<string> _mimeTypes = new(StringComparer.OrdinalIgnoreCase);

    public IntPtr Proxy { get; } = proxy;

    /// <summary>wl_data_offer.receive follows accept; the data-control offers start with receive.</summary>
    internal uint ReceiveOpcode => _mode is WaylandClipboardMode.Core ? 1u : 0u;

    private uint DestroyOpcode => _mode is WaylandClipboardMode.Core ? 2u : 1u;

    internal void AddMimeType(string mimeType) => _mimeTypes.Add(mimeType);

    internal string? SelectTextMimeType()
    {
        foreach (var preferred in PreferredTextMimeTypes)
        {
            if (_mimeTypes.TryGetValue(preferred, out var match))
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Tells the compositor this offer is no longer used, destroys the proxy and frees the listener.</summary>
    internal void Destroy()
    {
        if (IsDisposed)
        {
            return;
        }

        _ = _connection.Library.MarshalRequest(Proxy, DestroyOpcode, args: null, version: 1, flags: DestructorFlag);
        Dispose();
    }

    protected override void OnEvent(uint opcode, IntPtr args)
    {
        if (opcode is 0)
        {
            var mimePointer = Marshal.PtrToStructure<WlArgument>(args).s;
            _connection.AddOfferMimeType(Proxy, Marshal.PtrToStringUTF8(mimePointer) ?? string.Empty);
        }
    }
}
