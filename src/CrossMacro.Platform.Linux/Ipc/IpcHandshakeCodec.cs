using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Linux.Ipc;

internal static class IpcHandshakeCodec
{
    public static async Task<byte> ReadByteAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(stream, buffer, token).ConfigureAwait(false);
        return buffer[0];
    }

    public static async Task<int> ReadInt32Async(Stream stream, CancellationToken token)
    {
        var buffer = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, buffer, token).ConfigureAwait(false);
        return BitConverter.ToInt32(buffer);
    }

    public static async Task<string> ReadStringAsync(Stream stream, CancellationToken token)
    {
        var byteCount = await Read7BitEncodedIntAsync(stream, token).ConfigureAwait(false);
        if (byteCount < 0)
        {
            throw new IOException("Invalid handshake string length.");
        }

        if (byteCount == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[byteCount];
        await ReadExactlyAsync(stream, buffer, token).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task<int> Read7BitEncodedIntAsync(Stream stream, CancellationToken token)
    {
        var result = 0;
        var shift = 0;

        for (var index = 0; index < 5; index++)
        {
            var currentByte = await ReadByteAsync(stream, token).ConfigureAwait(false);
            result |= (currentByte & 0x7F) << shift;
            if ((currentByte & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new FormatException("Invalid 7-bit encoded handshake string length.");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Daemon closed the connection during handshake.");
            }

            offset += read;
        }
    }
}
