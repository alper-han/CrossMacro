using System.Buffers.Binary;
using System.IO.Compression;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public static class ScreenFramePngDecoder
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static ScreenFrame Decode(ReadOnlySpan<byte> pngBytes)
    {
        ScreenImageAssetPolicy.ValidateEncodedSize(pngBytes.Length);

        return DecodeCore(pngBytes);
    }

    public static bool TryValidatePng(ReadOnlySpan<byte> pngBytes, out int width, out int height, out string? error)
    {
        width = 0;
        height = 0;
        error = null;
        try
        {
            ScreenImageAssetPolicy.ValidateEncodedSize(pngBytes.Length);
            using var frame = DecodeCore(pngBytes);
            width = frame.Width;
            height = frame.Height;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ScreenFrame DecodeCore(ReadOnlySpan<byte> pngBytes)
    {

        if (pngBytes.Length < PngSignature.Length || !pngBytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            throw new InvalidDataException("Invalid PNG signature.");
        }

        PngHeader? header = null;
        using var idat = new MemoryStream();
        var offset = PngSignature.Length;
        var sawIend = false;
        var sawIdat = false;

        while (offset < pngBytes.Length)
        {
            if (pngBytes.Length - offset < 12)
            {
                throw new InvalidDataException("PNG chunk is truncated.");
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(pngBytes[offset..(offset + 4)]);
            if (length < 0)
            {
                throw new InvalidDataException("PNG chunk length is invalid.");
            }

            var type = pngBytes.Slice(offset + 4, 4);
            var dataStart = offset + 8;
            var nextOffset = checked(dataStart + length + 4);
            if (nextOffset < dataStart || nextOffset > pngBytes.Length)
            {
                throw new InvalidDataException("PNG chunk data is truncated.");
            }

            var data = pngBytes.Slice(dataStart, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(pngBytes[(nextOffset - 4)..nextOffset]);
            if (Crc32(type, data) != expectedCrc)
            {
                throw new InvalidDataException("PNG chunk CRC validation failed.");
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                if (header is not null || offset != PngSignature.Length)
                {
                    throw new InvalidDataException("PNG IHDR must be the first chunk and may appear only once.");
                }

                header = ReadHeader(data);
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (header is null || sawIend)
                {
                    throw new InvalidDataException("PNG IDAT chunk appears out of order.");
                }

                sawIdat = true;
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0)
                {
                    throw new InvalidDataException("PNG IEND chunk is invalid.");
                }

                if (!sawIdat)
                {
                    throw new InvalidDataException("PNG is missing IDAT data.");
                }

                sawIend = true;
                offset = nextOffset;
                break;
            }

            offset = nextOffset;
        }

        if (header is null)
        {
            throw new InvalidDataException("PNG is missing IHDR.");
        }

        if (idat.Length == 0)
        {
            throw new InvalidDataException("PNG is missing IDAT data.");
        }

        if (!sawIend || offset != pngBytes.Length)
        {
            throw new InvalidDataException("PNG is missing IEND.");
        }

        return DecodeImageData(header.Value, idat.ToArray());
    }

    public static bool TryReadDimensions(ReadOnlySpan<byte> pngBytes, out int width, out int height, out string? error)
    {
        width = 0;
        height = 0;
        error = null;
        try
        {
            using var frame = Decode(pngBytes);
            width = frame.Width;
            height = frame.Height;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static PngHeader ReadHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException("PNG IHDR length is invalid.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
        var height = BinaryPrimitives.ReadInt32BigEndian(data[4..8]);
        var bitDepth = data[8];
        var colorType = data[9];
        var compression = data[10];
        var filter = data[11];
        var interlace = data[12];

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG dimensions must be positive.");
        }

        ScreenImageAssetPolicy.ValidateDimensions(width, height);

        if (bitDepth != 8)
        {
            throw new NotSupportedException($"Unsupported PNG bit depth '{bitDepth}'. Only 8-bit PNG assets are supported.");
        }

        if (compression != 0 || filter != 0)
        {
            throw new NotSupportedException("Unsupported PNG compression or filter method.");
        }

        if (interlace != 0)
        {
            throw new NotSupportedException("Interlaced PNG assets are not supported.");
        }

        var channelCount = colorType switch
        {
            0 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type '{colorType}'. Supported types are grayscale, RGB, grayscale-alpha, and RGBA.")
        };

        return new PngHeader(width, height, colorType, channelCount);
    }

    private static ScreenFrame DecodeImageData(PngHeader header, byte[] compressedBytes)
    {
        var rowBytes = checked(header.Width * header.ChannelCount);
        var expectedBytes = checked((rowBytes + 1) * header.Height);
        var rgbBytes = checked(header.Width * header.Height * 3);
        if (expectedBytes > ScreenImageAssetPolicy.MaxInflatedBytes)
        {
            throw new InvalidDataException($"PNG decoded scanline data exceeds the maximum supported size of {ScreenImageAssetPolicy.MaxInflatedBytes} bytes.");
        }

        if (rgbBytes > ScreenImageAssetPolicy.MaxRgbBytes)
        {
            throw new InvalidDataException($"PNG decoded pixel data exceeds the maximum supported size of {ScreenImageAssetPolicy.MaxRgbBytes} bytes.");
        }

        var decompressed = new byte[expectedBytes];
        try
        {
            using (var input = new MemoryStream(compressedBytes, writable: false))
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            {
                var totalRead = 0;
                while (totalRead < decompressed.Length)
                {
                    var read = zlib.Read(decompressed, totalRead, decompressed.Length - totalRead);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead != expectedBytes || zlib.ReadByte() != -1)
                {
                    throw new InvalidDataException("PNG IDAT data length does not match IHDR dimensions.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("PNG IDAT zlib data is invalid.", ex);
        }

        var unfiltered = UnfilterScanlines(decompressed, header.Height, rowBytes, header.ChannelCount);
        var pixels = ConvertToRgb(header, unfiltered, rowBytes);
        return new ScreenFrame(
            new ScreenRect(0, 0, header.Width, header.Height),
            checked(header.Width * 3),
            ScreenPixelFormat.Rgb24,
            pixels);
    }

    private static byte[] UnfilterScanlines(byte[] source, int height, int rowBytes, int bytesPerPixel)
    {
        var output = new byte[checked(height * rowBytes)];
        var sourceOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filterType = source[sourceOffset++];
            var rowOffset = checked(y * rowBytes);
            var previousRowOffset = rowOffset - rowBytes;
            for (var x = 0; x < rowBytes; x++)
            {
                var raw = source[sourceOffset++];
                var left = x >= bytesPerPixel ? output[rowOffset + x - bytesPerPixel] : 0;
                var up = y > 0 ? output[previousRowOffset + x] : 0;
                var upperLeft = y > 0 && x >= bytesPerPixel ? output[previousRowOffset + x - bytesPerPixel] : 0;
                output[rowOffset + x] = filterType switch
                {
                    0 => raw,
                    1 => unchecked((byte)(raw + left)),
                    2 => unchecked((byte)(raw + up)),
                    3 => unchecked((byte)(raw + ((left + up) >> 1))),
                    4 => unchecked((byte)(raw + PaethPredictor(left, up, upperLeft))),
                    _ => throw new InvalidDataException($"Unsupported PNG filter type '{filterType}'.")
                };
            }
        }

        return output;
    }

    private static byte[] ConvertToRgb(PngHeader header, byte[] source, int rowBytes)
    {
        var pixels = new byte[checked(header.Width * header.Height * 3)];
        for (var y = 0; y < header.Height; y++)
        {
            var sourceRowOffset = checked(y * rowBytes);
            var targetRowOffset = checked(y * header.Width * 3);
            for (var x = 0; x < header.Width; x++)
            {
                var sourceOffset = sourceRowOffset + x * header.ChannelCount;
                var targetOffset = targetRowOffset + x * 3;
                switch (header.ColorType)
                {
                    case 0:
                        pixels[targetOffset] = source[sourceOffset];
                        pixels[targetOffset + 1] = source[sourceOffset];
                        pixels[targetOffset + 2] = source[sourceOffset];
                        break;
                    case 2:
                    case 6:
                        pixels[targetOffset] = source[sourceOffset];
                        pixels[targetOffset + 1] = source[sourceOffset + 1];
                        pixels[targetOffset + 2] = source[sourceOffset + 2];
                        break;
                    case 4:
                        pixels[targetOffset] = source[sourceOffset];
                        pixels[targetOffset + 1] = source[sourceOffset];
                        pixels[targetOffset + 2] = source[sourceOffset];
                        break;
                }
            }
        }

        return pixels;
    }

    private static int PaethPredictor(int left, int up, int upperLeft)
    {
        var estimate = left + up - upperLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upperLeftDistance = Math.Abs(estimate - upperLeft);
        if (leftDistance <= upDistance && leftDistance <= upperLeftDistance)
        {
            return left;
        }

        return upDistance <= upperLeftDistance ? up : upperLeft;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xFF];
        }

        foreach (var value in data)
        {
            crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] CrcTable = GenerateCrcTable();

    private static uint[] GenerateCrcTable()
    {
        var table = new uint[256];
        for (uint value = 0; value < table.Length; value++)
        {
            var crc = value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }

            table[value] = crc;
        }

        return table;
    }

    private readonly record struct PngHeader(int Width, int Height, byte ColorType, int ChannelCount);
}
