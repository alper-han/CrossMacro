using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class X11ScreenFrameProviderTests
{
    [Fact]
    public void X11Provider_WhenProbeIsSupported_ReportsSupported()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Supported());

        using var provider = new X11ScreenFrameProvider(capture);

        Assert.Equal("X11 XGetImage", provider.ProviderName);
        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void X11Provider_WhenSupportIsProvided_DoesNotProbeCaptureBackend()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Unsupported("probe should not run"));

        using var provider = new X11ScreenFrameProvider(capture, X11ScreenCaptureSupportResult.Supported());

        Assert.True(provider.IsSupported);
        Assert.Equal(0, capture.ProbeCalls);
    }

    [Fact]
    public async Task X11Provider_WhenUnsupported_ReportsUnavailableAndDoesNotCapture()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Unsupported("DISPLAY missing"));

        using var provider = new X11ScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY missing", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task X11Provider_WhenCaptureSucceeds_ReturnsSharedScreenFrame()
    {
        var owner = new CountingDisposable();
        var frame = new X11ScreenCaptureFrame(
            new ScreenRect(10, 20, 2, 1),
            8,
            ScreenPixelFormat.Xrgb8888,
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var capture = new FakeX11ScreenCapture(
            X11ScreenCaptureSupportResult.Supported(),
            X11ScreenCaptureResult.Success(frame));

        using var provider = new X11ScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(10, 20, 2, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(10, 20, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), resultFrame.GetPixel(new ScreenPoint(10, 20)));
        Assert.Equal(new ScreenPixelColor(0x44, 0x55, 0x66), resultFrame.GetPixel(new ScreenPoint(11, 20)));
        Assert.Equal(new ScreenRect(10, 20, 2, 1), capture.LastRegion);

        resultFrame.Dispose();
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task X11Provider_WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Supported());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var provider = new X11ScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task X11Provider_WhenCaptureThrowsTimeout_ReturnsStructuredTimeoutFailure()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Supported())
        {
            CaptureException = new TimeoutException("capture timed out")
        };

        using var provider = new X11ScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("capture timed out", result.ErrorMessage);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public void X11Provider_WhenDisposed_DisposesCaptureBackendOnce()
    {
        var capture = new FakeX11ScreenCapture(X11ScreenCaptureSupportResult.Supported());
        var provider = new X11ScreenFrameProvider(capture);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, capture.DisposeCount);
    }
}
