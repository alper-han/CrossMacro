using System.ComponentModel;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Windows.Services.ScreenReading;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Services.ScreenReading;

public sealed class WindowsScreenFrameProviderTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        using var provider = new WindowsScreenFrameProvider(new RecordingCaptureBackend(), () => true);

        Assert.Equal("Windows GDI BitBlt", provider.ProviderName);
    }

    [Fact]
    public void IsSupported_UsesInjectedPlatformProbe()
    {
        using var supported = new WindowsScreenFrameProvider(new RecordingCaptureBackend(), () => true);
        using var unsupported = new WindowsScreenFrameProvider(new RecordingCaptureBackend(), () => false);

        Assert.True(supported.IsSupported);
        Assert.False(unsupported.IsSupported);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenPlatformUnsupported_ReturnsUnsupportedWithoutCapturing()
    {
        var backend = new RecordingCaptureBackend();
        using var provider = new WindowsScreenFrameProvider(backend, () => false);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, result.ErrorKind);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionIsNull_CapturesVirtualScreenBounds()
    {
        var backend = new RecordingCaptureBackend
        {
            VirtualScreenBounds = new ScreenRect(-10, -20, 30, 40)
        };
        using var provider = new WindowsScreenFrameProvider(backend, () => true);

        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), backend.LastRegion);
        Assert.Equal(new ScreenRect(-10, -20, 30, 40), result.Value!.LogicalBounds);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionIsInsideVirtualScreen_ReturnsFrame()
    {
        var backend = new RecordingCaptureBackend
        {
            VirtualScreenBounds = new ScreenRect(-100, -50, 300, 200)
        };
        using var provider = new WindowsScreenFrameProvider(backend, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(-1, 2, 2, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new ScreenRect(-1, 2, 2, 1), result.Value!.LogicalBounds);
        Assert.Equal(ScreenPixelFormat.Bgra8888, result.Value.PixelFormat);
        Assert.Equal(8, result.Value.Stride);
        Assert.True(result.Value.TryGetPixel(new ScreenPoint(-1, 2), out var color));
        Assert.Equal(new ScreenPixelColor(0x33, 0x22, 0x11), color);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenRegionIsOutsideVirtualScreen_ReturnsOutOfBoundsWithoutCapturing()
    {
        var backend = new RecordingCaptureBackend
        {
            VirtualScreenBounds = new ScreenRect(0, 0, 10, 10)
        };
        using var provider = new WindowsScreenFrameProvider(backend, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(-1, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Equal(0, backend.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenCanceledBeforeCapture_ReturnsCanceledWithoutCapturing()
    {
        var backend = new RecordingCaptureBackend();
        using var provider = new WindowsScreenFrameProvider(backend, () => true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, backend.CaptureCalls);
    }

    public static TheoryData<Exception> KnownCaptureExceptions => new()
    {
        new ArgumentException("bad region"),
        new ArithmeticException("overflow"),
        new ExternalException("gdi failed"),
        new Win32Exception(5, "access denied"),
        new InvalidOperationException("invalid screen")
    };

    [Theory]
    [MemberData(nameof(KnownCaptureExceptions))]
    public async Task CaptureFrameAsync_WhenBackendThrowsKnownCaptureException_ReturnsCaptureFailed(Exception exception)
    {
        var backend = new RecordingCaptureBackend
        {
            CaptureException = exception
        };
        using var provider = new WindowsScreenFrameProvider(backend, () => true);

        var result = await provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains(exception.Message, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingCaptureBackend : IWindowsScreenCaptureBackend
    {
        public ScreenRect VirtualScreenBounds { get; init; } = new(0, 0, 100, 100);

        public ScreenRect? LastRegion { get; private set; }

        public int CaptureCalls { get; private set; }

        public Exception? CaptureException { get; init; }

        public ScreenRect GetVirtualScreenBounds() => VirtualScreenBounds;

        public WindowsScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureCalls++;
            LastRegion = region;

            if (CaptureException is { } exception)
            {
                throw exception;
            }

            var stride = checked(region.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
            var pixels = new byte[checked(stride * region.Height)];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = 0x11;
                pixels[offset + 1] = 0x22;
                pixels[offset + 2] = 0x33;
                pixels[offset + 3] = 0x00;
            }

            return new WindowsScreenCaptureFrame(region, stride, ScreenPixelFormat.Bgra8888, pixels);
        }
    }
}
