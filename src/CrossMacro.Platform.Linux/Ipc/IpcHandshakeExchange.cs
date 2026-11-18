namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// The client side of the daemon handshake. Shared by the long-lived IPC session and the one-shot
/// availability probe so both apply the same opcode and protocol version rules.
/// </summary>
internal static class IpcHandshakeExchange
{
    /// <exception cref="IpcClientException">The daemon rejected the handshake or speaks another protocol version.</exception>
    internal static async Task PerformAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(IpcHandshakeWireCodec.CreateHandshake(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var opcode = (IpcOpCode)await IpcHandshakeWireCodec.ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
        if (opcode is IpcOpCode.Error)
        {
            var message = await IpcHandshakeWireCodec.ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
            throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {message}");
        }

        if (opcode is not IpcOpCode.Handshake)
        {
            throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Unexpected handshake opcode: {opcode}");
        }

        var version = await IpcHandshakeWireCodec.ReadInt32Async(stream, cancellationToken).ConfigureAwait(false);
        if (version != IpcProtocol.ProtocolVersion)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ProtocolMismatch,
                $"Protocol version mismatch. Daemon: {version.ToString(CultureInfo.InvariantCulture)}, Client: {IpcProtocol.ProtocolVersion.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
