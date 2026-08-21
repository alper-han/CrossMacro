namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;


public sealed class ScreenshotCaptureServiceTests
{
    [Fact]
    public async Task CapturePngAsync_ReturnsValidatedPngBytesAndMetadata()
    {
        var provider = new FakeScreenFrameProvider();
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

        var result = await service.CapturePngAsync(
            new ScreenshotPngCaptureRequest(Region: new ScreenRect(1, 2, 2, 1)),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new ScreenRect(1, 2, 2, 1), provider.LastRegion);
        Assert.Equal(TimeSpan.FromSeconds(1), provider.LastOptions.Timeout);
        var data = Assert.IsType<ScreenshotPngCaptureData>(result.Data);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], data.PngBytes.Span[..4].ToArray());
        Assert.Equal(2, data.Width);
        Assert.Equal(1, data.Height);
        Assert.Equal("fake-frame", data.Provider);
        Assert.True(data.IsRegion);
        using var decoded = new ImageAssetCodec().DecodePng(data.PngBytes.Span);
        Assert.Equal(2, decoded.Width);
        Assert.Equal(1, decoded.Height);
    }

    [Fact]
    public async Task CapturePngAsync_WhenProviderIsUnsupported_ReturnsStructuredFailureBeforeCapture()
    {
        var provider = new FakeScreenFrameProvider { IsSupported = false };
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

        var result = await service.CapturePngAsync(new ScreenshotPngCaptureRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.ProviderUnsupported, result.FailureKind);
        Assert.Equal(0, provider.CaptureCalls);
    }

    [Fact]
    public async Task CapturePngAsync_WhenAlreadyCanceled_DoesNotCapture()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var provider = new FakeScreenFrameProvider();
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CapturePngAsync(
            new ScreenshotPngCaptureRequest(),
            cancellation.Token));

        Assert.Equal(0, provider.CaptureCalls);
    }

    [Fact]
    public async Task CaptureAsync_WhenWritingAndCopying_UsesOneCaptureAndOnePngEncoding()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-shot-{Guid.NewGuid():N}", "shot.png");
        try
        {
            var provider = new FakeScreenFrameProvider();
            var clipboard = new FakeImageClipboardService();
            var codec = new CountingImageAssetCodec();
            var service = new ScreenshotCaptureService(provider, clipboard, codec);

            var result = await service.CaptureAsync(outputPath, copyToClipboard: true, region: null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, provider.CaptureCalls);
            Assert.Equal(1, codec.EncodeCallCount);
            Assert.Equal(await File.ReadAllBytesAsync(outputPath), clipboard.PngBytes);
        }
        finally
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CapturePngAsync_WhenWritingAndCopying_ReturnsTheSamePngBytesAndDestinationMetadata()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-shot-{Guid.NewGuid():N}", "shot.png");
        try
        {
            var clipboard = new FakeImageClipboardService();
            var service = new ScreenshotCaptureService(new FakeScreenFrameProvider(), clipboard);

            var result = await service.CapturePngAsync(
                new ScreenshotPngCaptureRequest(
                    OutputPath: outputPath,
                    CopyToClipboard: true,
                    Region: new ScreenRect(4, 5, 2, 1)),
                CancellationToken.None);

            Assert.True(result.Success);
            var data = Assert.IsType<ScreenshotPngCaptureData>(result.Data);
            Assert.Equal(Path.GetFullPath(outputPath), data.OutputPath);
            Assert.True(data.CopiedToClipboard);
            Assert.True(data.IsRegion);
            Assert.Equal(data.PngBytes.ToArray(), await File.ReadAllBytesAsync(outputPath));
            Assert.Equal(data.PngBytes.ToArray(), clipboard.PngBytes);
        }
        finally
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CapturePngAsync_WhenImageClipboardIsUnsupported_FailsBeforeCapture()
    {
        var provider = new FakeScreenFrameProvider();
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService { IsSupported = false });

        var result = await service.CapturePngAsync(
            new ScreenshotPngCaptureRequest(CopyToClipboard: true),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.ClipboardUnsupported, result.FailureKind);
        Assert.Equal(0, provider.CaptureCalls);
    }

    [Fact]
    public async Task CapturePngAsync_WhenEncodingExceedsTheBound_ReturnsCaptureFailure()
    {
        var codec = new OversizedImageAssetCodec();
        var service = new ScreenshotCaptureService(new FakeScreenFrameProvider(), new FakeImageClipboardService(), codec);

        var result = await service.CapturePngAsync(new ScreenshotPngCaptureRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.CaptureFailed, result.FailureKind);
        Assert.Contains("encoding failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CapturePngAsync_WhenCodecUsesAnAlternateGrowthPath_EnforcesTheEncodedSizeBound(
        int growthValue)
    {
        var growth = (BoundedStreamGrowth)growthValue;
        var service = new ScreenshotCaptureService(
            new FakeScreenFrameProvider(),
            new FakeImageClipboardService(),
            new BoundedStreamGrowthImageAssetCodec(growth));

        var result = await service.CapturePngAsync(new ScreenshotPngCaptureRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.CaptureFailed, result.FailureKind);
        Assert.Contains("encoding failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureAsync_WritesPngToCreatedDirectoryAndReturnsCaptureData()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-shot-{Guid.NewGuid():N}", "shot.png");
        try
        {
            var provider = new FakeScreenFrameProvider();
            var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

            var result = await service.CaptureAsync(outputPath, copyToClipboard: false, new ScreenRect(1, 2, 2, 1), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new ScreenRect(1, 2, 2, 1), provider.LastRegion);
            Assert.Equal(TimeSpan.FromSeconds(1), provider.LastOptions.Timeout);
            Assert.True(File.Exists(outputPath));
            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
            var data = result.Data;
            Assert.NotNull(data);
            Assert.Equal(Path.GetFullPath(outputPath), data.OutputPath);
            Assert.Equal(2, data.Width);
            Assert.Equal(1, data.Height);
            Assert.Equal("png", data.Format);
            Assert.Equal("fake-frame", data.Provider);
            Assert.True(data.IsRegion);
            Assert.False(data.CopiedToClipboard);
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(outputPath)))
            {
                Directory.Delete(Path.GetDirectoryName(outputPath)!, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CaptureAsync_FileOnlyOutputDoesNotUseTheBoundedInMemoryPngPath()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-large-shot-{Guid.NewGuid():N}", "shot.png");
        try
        {
            var service = new ScreenshotCaptureService(
                new FakeScreenFrameProvider(),
                new FakeImageClipboardService(),
                new OversizedImageAssetCodec());

            var result = await service.CaptureAsync(outputPath, copyToClipboard: false, region: null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(new FileInfo(outputPath).Length > 48L * 1024 * 1024);
        }
        finally
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CaptureAsync_WhenClipboardRequested_CopiesPngBytes()
    {
        var clipboard = new FakeImageClipboardService();
        var service = new ScreenshotCaptureService(new FakeScreenFrameProvider(), clipboard);

        var result = await service.CaptureAsync(outputPath: null, copyToClipboard: true, region: null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(clipboard.PngBytes);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], clipboard.PngBytes[..4]);
        Assert.True(result.Data!.CopiedToClipboard);
    }

    [Fact]
    public async Task CaptureAsync_WhenFrameIsActuallyBlack_DoesNotRejectTheFrame()
    {
        var clipboard = new FakeImageClipboardService();
        var service = new ScreenshotCaptureService(
            new FakeScreenFrameProvider { Pixels = [0, 0, 0, 0, 0, 0] },
            clipboard);

        var result = await service.CaptureAsync(outputPath: null, copyToClipboard: true, region: null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(clipboard.PngBytes);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], clipboard.PngBytes[..4]);
    }

    [Fact]
    public async Task CaptureAsync_WhenProviderUnsupported_ReturnsSharedFailureShapeBeforeCapture()
    {
        var provider = new FakeScreenFrameProvider { IsSupported = false };
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

        var result = await service.CaptureAsync("shot.png", copyToClipboard: false, region: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.ProviderUnsupported, result.FailureKind);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, provider.CaptureCalls);
    }

    [Fact]
    public async Task CaptureAsync_WhenCaptureFails_ReturnsScreenReadErrorKind()
    {
        var service = new ScreenshotCaptureService(
            new FakeScreenFrameProvider
            {
                Failure = ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.PermissionDenied, "permission denied"),
            },
            new FakeImageClipboardService());

        var result = await service.CaptureAsync("shot.png", copyToClipboard: false, region: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.CaptureFailed, result.FailureKind);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ScreenReadErrorKind);
        Assert.Equal("permission denied", Assert.Single(result.Details));
    }

    [Fact]
    public async Task CaptureAsync_WhenImageClipboardToolIsMissing_ReturnsClipboardUnsupported()
    {
        var service = new ScreenshotCaptureService(
            new FakeScreenFrameProvider(),
            new FakeImageClipboardService { ThrowUnavailable = true });

        var result = await service.CaptureAsync(outputPath: null, copyToClipboard: true, region: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.ClipboardUnsupported, result.FailureKind);
        Assert.Equal("missing image clipboard tool", Assert.Single(result.Details));
    }

    private sealed class FakeScreenFrameProvider : IScreenFrameProvider
    {
        public string ProviderName => "fake-frame";
        public bool IsSupported { get; init; } = true;
        public ScreenReadResult<ScreenFrame>? Failure { get; init; }
        public byte[] Pixels { get; init; } = [0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00];
        public ScreenRect? LastRegion { get; private set; }
        public ScreenReadOptions LastOptions { get; private set; }
        public int CaptureCalls { get; private set; }

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            CaptureCalls++;
            LastRegion = region;
            LastOptions = options;
            if (Failure is { } failure)
            {
                return Task.FromResult(failure);
            }

            var bounds = region ?? new ScreenRect(0, 0, 2, 1);
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenFrame>(new ScreenFrame(
                bounds,
                bounds.Width * 3,
                ScreenPixelFormat.Rgb24,
                Pixels)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeImageClipboardService : IImageClipboardService
    {
        public bool IsSupported { get; init; } = true;
        public bool ThrowUnavailable { get; init; }
        public byte[]? PngBytes { get; private set; }

        public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
        {
            if (ThrowUnavailable)
            {
                throw new ImageClipboardUnavailableException("missing image clipboard tool");
            }

            PngBytes = pngBytes.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class CountingImageAssetCodec : IImageAssetCodec
    {
        private readonly ImageAssetCodec _inner = new();

        public int EncodeCallCount { get; private set; }

        public Task<byte[]> ReadFileAsync(string filePath, string? assetName = null, CancellationToken cancellationToken = default) =>
            _inner.ReadFileAsync(filePath, assetName, cancellationToken);

        public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default) =>
            _inner.DecodeFileAsync(filePath, cancellationToken);

        public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
            _inner.DecodePng(pngBytes, assetName);

        public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default) =>
            _inner.DecodePngAsync(pngBytes, assetName, cancellationToken);

        public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null) =>
            _inner.DecodeBase64Png(encoded, assetName);

        public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            _inner.DecodeBase64PngAsync(encoded, assetName, cancellationToken);

        public void ValidateBase64Png(string encoded, string? assetName = null) =>
            _inner.ValidateBase64Png(encoded, assetName);

        public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            _inner.ValidateBase64PngAsync(encoded, assetName, cancellationToken);

        public void ValidateMacroBudget(long totalEncodedBytes) => _inner.ValidateMacroBudget(totalEncodedBytes);

        public void EncodePng(ScreenFrame frame, Stream output)
        {
            EncodeCallCount++;
            _inner.EncodePng(frame, output);
        }

        public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default)
        {
            EncodeCallCount++;
            return _inner.EncodePngAsync(frame, output, cancellationToken);
        }
    }

    private sealed class OversizedImageAssetCodec : IImageAssetCodec
    {
        private const int OversizedLength = (48 * 1024 * 1024) + 1;

        public Task<byte[]> ReadFileAsync(string filePath, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateMacroBudget(long totalEncodedBytes) => throw new NotSupportedException();

        public void EncodePng(ScreenFrame frame, Stream output) => throw new NotSupportedException();

        public async Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default)
        {
            var chunk = new byte[8192];
            for (var written = 0; written < OversizedLength; written += chunk.Length)
            {
                await output.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private enum BoundedStreamGrowth
    {
        WriteByte,
        SetLength,
    }

    private sealed class BoundedStreamGrowthImageAssetCodec(BoundedStreamGrowth growth) : IImageAssetCodec
    {
        public Task<byte[]> ReadFileAsync(string filePath, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null) =>
            throw new NotSupportedException();

        public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateMacroBudget(long totalEncodedBytes) => throw new NotSupportedException();

        public void EncodePng(ScreenFrame frame, Stream output) => throw new NotSupportedException();

        public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default)
        {
            switch (growth)
            {
                case BoundedStreamGrowth.WriteByte:
                    _ = output.Seek(ScreenshotPngCaptureLimits.MaximumEncodedBytes, SeekOrigin.Begin);
                    output.WriteByte(0);
                    break;
                case BoundedStreamGrowth.SetLength:
                    output.SetLength((long)ScreenshotPngCaptureLimits.MaximumEncodedBytes + 1);
                    break;
                default:
                    throw new InvalidOperationException("Unknown stream growth operation.");
            }

            return Task.CompletedTask;
        }
    }
}
