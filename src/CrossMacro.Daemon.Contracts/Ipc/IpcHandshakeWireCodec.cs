using System.Buffers.Binary;
using System.Text;

namespace CrossMacro.Daemon.Contracts.Ipc;

/// <summary>Protocol handshake encoding shared by sessions and diagnostic probes.</summary>
public static class IpcHandshakeWireCodec
{
    public static byte[] CreateRequest(int version = IpcProtocol.ProtocolVersion)
    {
        var payload = new byte[sizeof(byte) + sizeof(int)];
        payload[0] = (byte)IpcOpCode.Handshake;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), version);
        return payload;
    }

    public static byte ReadByte(Stream stream, Action? beforeRead = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Span<byte> buffer = stackalloc byte[1];
        ReadExactly(stream, buffer, beforeRead);
        return buffer[0];
    }

    public static int ReadInt32(Stream stream, Action? beforeRead = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadExactly(stream, buffer, beforeRead);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static string ReadString(Stream stream, Action? beforeRead = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var length = ReadStringLength(() => ReadByte(stream, beforeRead));
        var buffer = new byte[length];
        ReadExactly(stream, buffer, beforeRead);
        return Encoding.UTF8.GetString(buffer);
    }

    public static async Task<byte> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new byte[1];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer[0];
    }

    public static async Task<int> ReadInt32Async(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static async Task<string> ReadStringAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var encodedLength = 0u;
        for (var index = 0; index < 5; index++)
        {
            var next = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
            encodedLength |= (uint)(next & 0x7F) << (index * 7);
            if ((next & 0x80) is 0)
            {
                var length = ValidateStringLength(encodedLength, next, index);
                var buffer = new byte[length];
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
                return Encoding.UTF8.GetString(buffer);
            }
        }

        throw new FormatException("Invalid 7-bit encoded handshake string length.");
    }

    private static int ReadStringLength(Func<byte> readByte)
    {
        var encodedLength = 0u;
        for (var index = 0; index < 5; index++)
        {
            var next = readByte();
            encodedLength |= (uint)(next & 0x7F) << (index * 7);
            if ((next & 0x80) is 0)
            {
                return ValidateStringLength(encodedLength, next, index);
            }
        }

        throw new FormatException("Invalid 7-bit encoded handshake string length.");
    }

    private static int ValidateStringLength(uint length, byte lastByte, int index)
    {
        if (length > int.MaxValue || (index is 4 && lastByte > 7))
        {
            throw new IOException("Invalid handshake string length.");
        }
        return (int)length;
    }

    private static void ReadExactly(Stream stream, Span<byte> destination, Action? beforeRead)
    {
        while (!destination.IsEmpty)
        {
            beforeRead?.Invoke();
            var count = stream.Read(destination);
            if (count is 0)
            {
                throw new EndOfStreamException("Daemon closed the connection during handshake.");
            }
            destination = destination[count..];
        }
    }
}
