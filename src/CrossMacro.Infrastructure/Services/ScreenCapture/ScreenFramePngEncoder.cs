
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public static class ScreenFramePngEncoder
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Encode(ScreenFrame frame, Stream output)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(PngSignature);
        WriteIhdr(output, frame.Width, frame.Height);
        WriteIdat(output, frame);
        WriteIend(output);
    }

    public static async Task EncodeAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteAsync(PngSignature, cancellationToken).ConfigureAwait(false);
        await WriteIhdrAsync(output, frame.Width, frame.Height, cancellationToken).ConfigureAwait(false);
        await WriteIdatAsync(output, frame, cancellationToken).ConfigureAwait(false);
        await WriteIendAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteIhdrAsync(Stream output, int width, int height, CancellationToken cancellationToken)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        data[8] = 8;
        data[9] = 2;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        await WriteChunkAsync(output, "IHDR"u8.ToArray(), data, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteIhdr(Stream output, int width, int height)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data[4..], height);
        data[8] = 8;
        data[9] = 2;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        WriteChunk(output, "IHDR"u8, data);
    }

    private static async Task WriteIdatAsync(Stream output, ScreenFrame frame, CancellationToken cancellationToken)
    {
        using var idatBuffer = new MemoryStream();
        uint adler32;
        using (var deflate = new DeflateStream(idatBuffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            adler32 = await WriteFilteredScanlinesAsync(deflate, frame, cancellationToken).ConfigureAwait(false);
        }

        using var zlibBuffer = new MemoryStream();
        zlibBuffer.WriteByte(0x78);
        zlibBuffer.WriteByte(0x01);
        idatBuffer.Position = 0;
        await idatBuffer.CopyToAsync(zlibBuffer, cancellationToken).ConfigureAwait(false);

        var adler = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, adler32);
        await zlibBuffer.WriteAsync(adler, cancellationToken).ConfigureAwait(false);

        await WriteChunkAsync(output, "IDAT"u8.ToArray(), zlibBuffer.GetBuffer().AsMemory(0, (int)zlibBuffer.Length), cancellationToken).ConfigureAwait(false);
    }

    private static void WriteIdat(Stream output, ScreenFrame frame)
    {
        using var idatBuffer = new MemoryStream();
        uint adler32;
        using (var deflate = new DeflateStream(idatBuffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            adler32 = WriteFilteredScanlines(deflate, frame);
        }

        using var zlibBuffer = new MemoryStream();
        zlibBuffer.WriteByte(0x78);
        zlibBuffer.WriteByte(0x01);
        idatBuffer.Position = 0;
        idatBuffer.CopyTo(zlibBuffer);

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, adler32);
        zlibBuffer.Write(adler);
        WriteChunk(output, "IDAT"u8, zlibBuffer.GetBuffer().AsSpan(0, (int)zlibBuffer.Length));
    }

    private static Task WriteIendAsync(Stream output, CancellationToken cancellationToken) =>
        WriteChunkAsync(output, "IEND"u8.ToArray(), ReadOnlyMemory<byte>.Empty, cancellationToken);

    private static void WriteIend(Stream output) => WriteChunk(output, "IEND"u8, []);

    private static async Task<uint> WriteFilteredScanlinesAsync(Stream deflate, ScreenFrame frame, CancellationToken cancellationToken)
    {
        uint a = 1, b = 0;
        var pixels = frame.Pixels.ToArray();
        var bpp = ScreenFrame.GetBytesPerPixel(frame.PixelFormat);
        var rgbRow = new byte[frame.Width * 3];
        var filterByte = new byte[1];

        for (var y = 0; y < frame.Height; y++)
        {
            await deflate.WriteAsync(filterByte, cancellationToken).ConfigureAwait(false);
            UpdateAdler(ref a, ref b, 0);

            var rowOffset = y * frame.Stride;
            ConvertRowToRgb(pixels, rowOffset, frame.Width, bpp, frame.PixelFormat, rgbRow);
            await deflate.WriteAsync(rgbRow, cancellationToken).ConfigureAwait(false);

            foreach (var value in rgbRow)
            {
                UpdateAdler(ref a, ref b, value);
            }
        }

        return (b << 16) | a;
    }

    private static uint WriteFilteredScanlines(Stream deflate, ScreenFrame frame)
    {
        uint a = 1, b = 0;
        var pixels = frame.Pixels.Span;
        var bpp = ScreenFrame.GetBytesPerPixel(frame.PixelFormat);
        var rgbRow = new byte[frame.Width * 3];

        for (var y = 0; y < frame.Height; y++)
        {
            deflate.WriteByte(0);
            UpdateAdler(ref a, ref b, 0);
            var rowOffset = y * frame.Stride;
            ConvertRowToRgb(pixels, rowOffset, frame.Width, bpp, frame.PixelFormat, rgbRow);
            deflate.Write(rgbRow, 0, rgbRow.Length);
            foreach (var value in rgbRow)
            {
                UpdateAdler(ref a, ref b, value);
            }
        }

        return (b << 16) | a;
    }

    private static void ConvertRowToRgb(ReadOnlySpan<byte> pixels, int rowOffset, int width, int bpp, ScreenPixelFormat format, byte[] rgb)
    {
        for (var x = 0; x < width; x++)
        {
            var srcOffset = rowOffset + (x * bpp);
            var dstOffset = x * 3;

            switch (format)
            {
                case ScreenPixelFormat.Rgb24:
                case ScreenPixelFormat.Abgr8888:
                case ScreenPixelFormat.Xbgr8888:
                    rgb[dstOffset] = pixels[srcOffset];
                    rgb[dstOffset + 1] = pixels[srcOffset + 1];
                    rgb[dstOffset + 2] = pixels[srcOffset + 2];
                    break;
                case ScreenPixelFormat.Bgr24:
                case ScreenPixelFormat.Xrgb8888:
                case ScreenPixelFormat.Bgra8888:
                    rgb[dstOffset] = pixels[srcOffset + 2];
                    rgb[dstOffset + 1] = pixels[srcOffset + 1];
                    rgb[dstOffset + 2] = pixels[srcOffset];
                    break;
                default:
                    throw new NotSupportedException($"Unsupported pixel format: {format}");
            }
        }
    }

    private static void UpdateAdler(ref uint a, ref uint b, byte value)
    {
        a = (a + value) % 65521;
        b = (b + a) % 65521;
    }

    private static async Task WriteChunkAsync(Stream output, ReadOnlyMemory<byte> type, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes.AsSpan(), data.Length);
        await output.WriteAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(type, cancellationToken).ConfigureAwait(false);

        if (data.Length > 0)
        {
            await output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        var crc = Crc32(type.Span, data.Span);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes.AsSpan(), crc);
        await output.WriteAsync(crcBytes, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
        output.Write(lengthBytes);
        output.Write(type);
        if (data.Length > 0)
        {
            output.Write(data);
        }

        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
        }

        foreach (var b in data)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = GenerateCrcTable();

    private static uint[] GenerateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
