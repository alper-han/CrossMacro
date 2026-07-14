namespace CrossMacro.Infrastructure.Tests.Services.ScreenCapture;

using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;

public sealed class ScreenshotCaptureServiceTests
{
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
    public async Task CaptureAsync_WhenClipboardRequested_CopiesPngBytes()
    {
        var clipboard = new FakeImageClipboardService();
        var service = new ScreenshotCaptureService(new FakeScreenFrameProvider(), clipboard);

        var result = await service.CaptureAsync(null, copyToClipboard: true, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(clipboard.PngBytes);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], clipboard.PngBytes[..4]);
        Assert.True(result.Data!.CopiedToClipboard);
    }

    [Fact]
    public async Task CaptureAsync_WhenProviderUnsupported_ReturnsSharedFailureShapeBeforeCapture()
    {
        var provider = new FakeScreenFrameProvider { IsSupported = false };
        var service = new ScreenshotCaptureService(provider, new FakeImageClipboardService());

        var result = await service.CaptureAsync("shot.png", copyToClipboard: false, null, CancellationToken.None);

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
                Failure = ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.PermissionDenied, "permission denied")
            },
            new FakeImageClipboardService());

        var result = await service.CaptureAsync("shot.png", copyToClipboard: false, null, CancellationToken.None);

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

        var result = await service.CaptureAsync(null, copyToClipboard: true, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ScreenshotCaptureFailureKind.ClipboardUnsupported, result.FailureKind);
        Assert.Equal("missing image clipboard tool", Assert.Single(result.Details));
    }

    private sealed class FakeScreenFrameProvider : IScreenFrameProvider
    {
        public string ProviderName => "fake-frame";
        public bool IsSupported { get; init; } = true;
        public ScreenReadResult<ScreenFrame>? Failure { get; init; }
        public ScreenRect? LastRegion { get; private set; }
        public int CaptureCalls { get; private set; }

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            CaptureCalls++;
            LastRegion = region;
            if (Failure is { } failure)
            {
                return Task.FromResult(failure);
            }

            var bounds = region ?? new ScreenRect(0, 0, 2, 1);
            byte[] pixels = [0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00];
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(new ScreenFrame(
                bounds,
                bounds.Width * 3,
                ScreenPixelFormat.Rgb24,
                pixels)));
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
}
