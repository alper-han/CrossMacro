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
                0x00, 0xFF, 0x00, 0xFF,
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

    [Fact]
    public void Decode_WhenEncoderProducedRgbPng_ReturnsRgb24FrameWithMatchingPixels()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Bgra8888,
            new byte[]
            {
                0x00, 0x00, 0xFF, 0xFF,
                0x00, 0xFF, 0x00, 0xFF,
            });
        using var png = new MemoryStream();
        ScreenFramePngEncoder.Encode(source, png);

        using var decoded = ScreenFramePngDecoder.Decode(png.ToArray());

        Assert.Equal(new ScreenRect(0, 0, 2, 1), decoded.LogicalBounds);
        Assert.Equal(ScreenPixelFormat.Rgb24, decoded.PixelFormat);
        Assert.Equal(new ScreenPixelColor(0xFF, 0x00, 0x00), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0x00, 0xFF, 0x00), decoded.GetPixel(new ScreenPoint(1, 0)));
    }

    [Fact]
    public void Decode_WhenBytesAreNotPng_ThrowsClearFailure()
    {
        var act = () => ScreenFramePngDecoder.Decode([0x00, 0x01, 0x02]);

        Assert.Throws<InvalidDataException>(act);
    }

    [Fact]
    public void Decode_WhenDimensionsExceedSupportedLimit_ThrowsInvalidDataException()
    {
        var act = () => ScreenFramePngDecoder.Decode(CreateOversizedPngBytes());

        var exception = Assert.Throws<InvalidDataException>(act);
        Assert.Contains("maximum supported size of 7680x4320", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_WhenIendIsMissing_ThrowsInvalidDataException()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);

        var act = () => ScreenFramePngDecoder.Decode(png[..^12]);

        Assert.Throws<InvalidDataException>(act);
    }

    [Fact]
    public void Decode_WhenIdatZlibDataIsCorrupt_ThrowsInvalidDataException()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0xFF;

        var act = () => ScreenFramePngDecoder.Decode(png);

        Assert.Throws<InvalidDataException>(act);
    }

    [Fact]
    public void Decode_WhenChunkCrcIsInvalid_RejectsTheAsset()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0x01;

        var exception = Assert.Throws<InvalidDataException>(() => ScreenFramePngDecoder.Decode(png));

        Assert.Contains("CRC", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Policy_WhenChunkCrcIsInvalid_ReportsValidationFailure()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0x01;

        var valid = ScreenImageAssetPolicy.TryValidateEncodedPng(png, out var error);

        Assert.False(valid);
        Assert.Contains("CRC", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Policy_WhenPngIsMissingIdat_RejectsItBeforePersistence()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        var idatStart = Array.IndexOf(png, (byte)'I', 8);
        var truncated = png[..idatStart];

        Assert.False(ScreenImageAssetPolicy.TryValidateEncodedPng(truncated, out var error));
        Assert.Contains("truncated", error, StringComparison.OrdinalIgnoreCase);
    }

    private const string TransparentPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=";

    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x6C, 0xF7, 0xBC, 0x13,
        ];
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
