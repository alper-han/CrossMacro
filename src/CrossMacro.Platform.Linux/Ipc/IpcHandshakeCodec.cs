namespace CrossMacro.Platform.Linux.Ipc;

// Retains the internal client entry point while the wire implementation is shared
// with the daemon diagnostic probe and server contract assembly.
internal static class IpcHandshakeCodec
{
    public static Task<byte> ReadByteAsync(Stream stream, CancellationToken token) =>
        IpcHandshakeWireCodec.ReadByteAsync(stream, token);

    public static Task<int> ReadInt32Async(Stream stream, CancellationToken token) =>
        IpcHandshakeWireCodec.ReadInt32Async(stream, token);

    public static Task<string> ReadStringAsync(Stream stream, CancellationToken token) =>
        IpcHandshakeWireCodec.ReadStringAsync(stream, token);
}
