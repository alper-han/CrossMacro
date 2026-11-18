
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public static class ScreenFramePngEncoder
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Encode(ScreenFrame frame, Stream output)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(output);
        Encode(frame, output, CancellationToken.None);
    }

    public static async Task EncodeAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(output);

        using var encoded = new MemoryStream();
        await Task.Run(() => Encode(frame, encoded, cancellationToken), cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(encoded.GetBuffer().AsMemory(0, (int)encoded.Length), cancellationToken).ConfigureAwait(false);
    }

    private static void Encode(ScreenFrame frame, Stream output, CancellationToken cancellationToken)
    {
        ValidateFrame(frame);

        var hasAlpha = UsesAlpha(frame);
        output.Write(PngSignature);
        WriteIhdr(output, frame.Width, frame.Height, hasAlpha);

        using var zlibData = new MemoryStream();
        CompressScanlines(frame, hasAlpha, zlibData, cancellationToken);
        WriteChunk(output, "IDAT"u8, zlibData.GetBuffer().AsSpan(0, (int)zlibData.Length));
        WriteChunk(output, "IEND"u8, []);
    }

    private static void WriteIhdr(Stream output, int width, int height, bool hasAlpha)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data[4..], height);
        data[8] = 8;
        data[9] = hasAlpha ? (byte)6 : (byte)2;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        WriteChunk(output, "IHDR"u8, data);
    }

    private static void CompressScanlines(ScreenFrame frame, bool hasAlpha, MemoryStream destination, CancellationToken cancellationToken)
    {
        var pixels = frame.Pixels.Span;
        var layout = ScreenPixelFormatLayout.Get(frame.PixelFormat);
        var premultiplied = hasAlpha && frame.AlphaMode is ScreenAlphaMode.Premultiplied;
        var scanline = new byte[checked((frame.Width * (hasAlpha ? 4 : 3)) + 1)];

        using var zlib = new ZLibStream(destination, CompressionLevel.Fastest, leaveOpen: true);
        for (var y = 0; y < frame.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConvertRow(pixels[(y * frame.Stride)..], scanline.AsSpan(1), frame.Width, layout, hasAlpha, premultiplied);
            zlib.Write(scanline);
        }
    }

    private static void ConvertRow(ReadOnlySpan<byte> source, Span<byte> target, int width, ScreenPixelLayout layout, bool hasAlpha, bool premultiplied)
    {
        var targetStride = hasAlpha ? 4 : 3;
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * layout.BytesPerPixel;
            var targetOffset = x * targetStride;
            var red = source[sourceOffset + layout.RedOffset];
            var green = source[sourceOffset + layout.GreenOffset];
            var blue = source[sourceOffset + layout.BlueOffset];
            if (hasAlpha)
            {
                var alpha = source[sourceOffset + layout.AlphaOffset];
                if (premultiplied)
                {
                    red = Unpremultiply(red, alpha);
                    green = Unpremultiply(green, alpha);
                    blue = Unpremultiply(blue, alpha);
                }

                target[targetOffset + 3] = alpha;
            }

            target[targetOffset] = red;
            target[targetOffset + 1] = green;
            target[targetOffset + 2] = blue;
        }
    }

    private static bool UsesAlpha(ScreenFrame frame) =>
        frame.HasAlphaChannel
        && (frame.AlphaMode is ScreenAlphaMode.Straight or ScreenAlphaMode.Premultiplied);

    private static void ValidateFrame(ScreenFrame frame)
    {
        ScreenImageAssetPolicy.ValidateDimensions(frame.Width, frame.Height);
        var bytesPerPixel = UsesAlpha(frame) ? 4 : 3;
        var pixelBytes = checked((long)frame.Width * frame.Height * bytesPerPixel);
        if (pixelBytes > ScreenImageAssetPolicy.MaxPixelBytes)
        {
            throw new InvalidDataException($"PNG pixel data exceeds the maximum supported size of {ScreenImageAssetPolicy.MaxPixelBytes} bytes.");
        }
    }

    private static byte Unpremultiply(byte value, byte alpha) => alpha is 0 ? (byte)0 : (byte)Math.Min(byte.MaxValue, ((value * 255) + (alpha / 2)) / alpha);

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

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, PngChunkCrc.Compute(type, data));
        output.Write(crcBytes);
    }
}
