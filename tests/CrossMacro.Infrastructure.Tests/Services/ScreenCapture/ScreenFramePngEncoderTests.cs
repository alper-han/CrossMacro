namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;

using System.Buffers.Binary;
using System.IO.Compression;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;

public sealed class ScreenFramePngEncoderTests
{
    [Fact]
    public void Encode_WhenFrameIsBgra_WritesValidRgbPngPayload()
    {
        using var frame = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Bgra8888,
            new byte[]
            {
                0x00, 0x00, 0xFF, 0xFF,
                0x00, 0xFF, 0x00, 0xFF
            });
        using var png = new MemoryStream();

        ScreenFramePngEncoder.Encode(frame, png);

        var bytes = png.ToArray();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);

        var idat = ReadChunk(bytes, "IDAT"u8);
        using var idatStream = new MemoryStream(idat);
        using var zlib = new ZLibStream(idatStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlib.CopyTo(decompressed);

        Assert.Equal([0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00], decompressed.ToArray());
    }

    private static byte[] ReadChunk(byte[] png, ReadOnlySpan<byte> chunkType)
    {
        var offset = 8;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = png.AsSpan(offset + 4, 4);
            if (type.SequenceEqual(chunkType))
            {
                return png.AsSpan(offset + 8, length).ToArray();
            }

            offset += 12 + length;
        }

        throw new InvalidOperationException("PNG chunk was not found.");
    }
}
