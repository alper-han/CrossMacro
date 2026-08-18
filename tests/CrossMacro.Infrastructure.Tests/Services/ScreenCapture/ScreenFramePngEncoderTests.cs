namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;


public sealed class ScreenFramePngEncoderTests
{
    [Fact]
    public async Task Encode_WhenFrameIsBgra_WritesValidRgbPngPayload()
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

        await ScreenFramePngEncoder.EncodeAsync(frame, png, CancellationToken.None);

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
    public void CompatibilityApis_EncodeDecodeAndValidatePngSynchronously()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[] { 0x12, 0x34, 0x56 });
        using var encoded = new MemoryStream();
        IImageAssetCodec codec = new ImageAssetCodec();

        codec.EncodePng(source, encoded);
        var png = encoded.ToArray();
        using var decoded = codec.DecodePng(png, "compatibility");
        codec.ValidateBase64Png(Convert.ToBase64String(png), "compatibility");
        IImageAssetPreviewDecoder previewDecoder = new ImageAssetPreviewDecoder(codec);
        var preview = previewDecoder.Decode(Convert.ToBase64String(png), "compatibility");

        Assert.Equal(new ScreenPixelColor(0x12, 0x34, 0x56), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(1, preview.Width);
        Assert.Equal(1, preview.Height);
    }

    [Fact]
    public async Task DecodeAsync_WhenCanceledAtDecodePath_ThrowsCancellation()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => ScreenFramePngDecoder.DecodeAsync(png, cancellation.Token));
    }

    [Fact]
    public async Task Decode_WhenEncoderProducedRgbPng_ReturnsRgb24FrameWithMatchingPixels()
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
        await ScreenFramePngEncoder.EncodeAsync(source, png, CancellationToken.None);

        using var decoded = await ScreenFramePngDecoder.DecodeAsync(png.ToArray(), CancellationToken.None);

        Assert.Equal(new ScreenRect(0, 0, 2, 1), decoded.LogicalBounds);
        Assert.Equal(ScreenPixelFormat.Rgb24, decoded.PixelFormat);
        Assert.Equal(new ScreenPixelColor(0xFF, 0x00, 0x00), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0x00, 0xFF, 0x00), decoded.GetPixel(new ScreenPoint(1, 0)));
    }

    [Fact]
    public async Task EncodeDecode_WhenFrameHasStraightAlpha_PreservesRgbaPixels()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 2, 1),
            stride: 8,
            ScreenPixelFormat.Abgr8888,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0x00,
                0x00, 0xFF, 0x00, 0x80,
            },
            alphaMode: ScreenAlphaMode.Straight);
        using var png = new MemoryStream();

        await ScreenFramePngEncoder.EncodeAsync(source, png, CancellationToken.None);
        using var decoded = await ScreenFramePngDecoder.DecodeAsync(png.ToArray(), CancellationToken.None);

        Assert.Equal(ScreenPixelFormat.Abgr8888, decoded.PixelFormat);
        Assert.Equal(ScreenAlphaMode.Straight, decoded.AlphaMode);
        Assert.True(decoded.TryGetAlpha(new ScreenPoint(0, 0), out var transparentAlpha));
        Assert.True(decoded.TryGetAlpha(new ScreenPoint(1, 0), out var translucentAlpha));
        Assert.Equal(0, transparentAlpha);
        Assert.Equal(0x80, translucentAlpha);
        Assert.Equal(new ScreenPixelColor(0xFF, 0x00, 0x00), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0x00, 0xFF, 0x00), decoded.GetPixel(new ScreenPoint(1, 0)));
    }

    [Fact]
    public async Task Decode_WhenPngUsesGrayscaleAlpha_PreservesAlphaAndExpandsGrayChannel()
    {
        var png = CreatePng(
            width: 2,
            height: 1,
            colorType: 4,
            filteredScanlines: [0, 0x40, 0x80, 0xC0, 0xFF]);

        using var decoded = await ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None);

        Assert.Equal(ScreenPixelFormat.Abgr8888, decoded.PixelFormat);
        Assert.Equal(ScreenAlphaMode.Straight, decoded.AlphaMode);
        Assert.Equal(new ScreenPixelColor(0x40, 0x40, 0x40), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0xC0, 0xC0, 0xC0), decoded.GetPixel(new ScreenPoint(1, 0)));
        Assert.True(decoded.TryGetAlpha(new ScreenPoint(0, 0), out var firstAlpha));
        Assert.True(decoded.TryGetAlpha(new ScreenPoint(1, 0), out var secondAlpha));
        Assert.Equal(0x80, firstAlpha);
        Assert.Equal(0xFF, secondAlpha);
    }

    [Fact]
    public async Task EncodeDecode_WhenFrameIsPremultiplied_PreservesStraightPngValues()
    {
        using var source = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 4,
            ScreenPixelFormat.Bgra8888,
            new byte[] { 20, 40, 80, 128 },
            alphaMode: ScreenAlphaMode.Premultiplied);
        using var png = new MemoryStream();

        await ScreenFramePngEncoder.EncodeAsync(source, png, CancellationToken.None);
        using var decoded = await ScreenFramePngDecoder.DecodeAsync(png.ToArray(), CancellationToken.None);

        Assert.Equal(ScreenAlphaMode.Straight, decoded.AlphaMode);
        Assert.Equal(new ScreenPixelColor(159, 80, 40), decoded.GetPixel(new ScreenPoint(0, 0)));
        Assert.True(decoded.TryGetAlpha(new ScreenPoint(0, 0), out var alpha));
        Assert.Equal(128, alpha);
    }

    [Fact]
    public async Task Decode_WhenPngUsesTrnsTransparency_ReportsUnsupportedFormat()
    {
        var png = CreatePng(
            width: 1,
            height: 1,
            colorType: 2,
            filteredScanlines: [0, 0x10, 0x20, 0x30],
            [ ("tRNS", new byte[] { 0x00, 0x10, 0x00, 0x20, 0x00, 0x30 }) ]);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None));

        Assert.Contains("tRNS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WhenPngUsesUnknownCriticalChunk_ReportsUnsupportedFormat()
    {
        var png = CreatePng(
            width: 1,
            height: 1,
            colorType: 2,
            filteredScanlines: [0, 0x10, 0x20, 0x30],
            [ ("ABCD", Array.Empty<byte>()) ]);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None));

        Assert.Contains("ABCD", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WhenInflatedScanlinesContainTrailingData_RejectsThePng()
    {
        var png = CreatePng(
            width: 1,
            height: 1,
            colorType: 2,
            filteredScanlines: [0, 0x10, 0x20, 0x30, 0xFF]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None));

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_WhenBytesAreNotPng_ThrowsClearFailure()
    {
        static Task<ScreenFrame> act() => ScreenFramePngDecoder.DecodeAsync(new byte[] { 0x00, 0x01, 0x02 }, CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidDataException>(act);
    }

    [Fact]
    public async Task Decode_WhenDimensionsExceedSupportedLimit_ThrowsInvalidDataException()
    {
        static Task<ScreenFrame> act() => ScreenFramePngDecoder.DecodeAsync(CreateOversizedPngBytes(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(act);
        Assert.Contains("maximum supported size of 7680x4320", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decode_WhenIendIsMissing_ThrowsInvalidDataException()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);

        Task<ScreenFrame> actAsync() => ScreenFramePngDecoder.DecodeAsync(png.AsMemory(0, png.Length - 12), CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidDataException>(actAsync);
    }

    [Fact]
    public async Task Decode_WhenIdatZlibDataIsCorrupt_ThrowsInvalidDataException()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0xFF;

        Task<ScreenFrame> actAsync() => ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidDataException>(actAsync);
    }

    [Fact]
    public async Task Decode_WhenChunkCrcIsInvalid_RejectsTheAsset()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0x01;

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ScreenFramePngDecoder.DecodeAsync(png, CancellationToken.None));

        Assert.Contains("CRC", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Policy_WhenChunkCrcIsInvalid_ReportsValidationFailure()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        png[41] ^= 0x01;

        var validation = await ScreenImageAssetPolicy.TryValidateEncodedPngAsync(png, cancellationToken: CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Contains("CRC", validation.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Policy_WhenPngIsMissingIdat_RejectsItBeforePersistence()
    {
        var png = Convert.FromBase64String(TransparentPngBase64);
        var idatStart = Array.IndexOf(png, (byte)'I', 8);
        var truncated = png[..idatStart];

        var validation = await ScreenImageAssetPolicy.TryValidateEncodedPngAsync(truncated, cancellationToken: CancellationToken.None);
        Assert.False(validation.IsValid);
        Assert.Contains("truncated", validation.Error, StringComparison.OrdinalIgnoreCase);
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

    private static byte[] CreatePng(
        int width,
        int height,
        byte colorType,
        ReadOnlySpan<byte> filteredScanlines,
        params (string Type, byte[] Data)[] chunksBeforeIdat)
    {
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = colorType;

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(filteredScanlines);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR"u8, header);
        foreach (var (type, data) in chunksBeforeIdat)
        {
            WriteChunk(png, System.Text.Encoding.ASCII.GetBytes(type), data);
        }

        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc32(type, data));
        output.Write(crc);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
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

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint value = 0; value < table.Length; value++)
        {
            var crc = value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) is not 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }

            table[value] = crc;
        }

        return table;
    }
}
