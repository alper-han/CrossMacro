using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class WlrScreencopyScreenFrameProviderTests
{
    [Fact]
    public void WlrScreencopyProvider_WhenProbeIsSupported_ReportsSupported()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Supported());

        using var provider = new WlrScreencopyScreenFrameProvider(capture);

        Assert.Equal("Wayland wlr-screencopy-unstable-v1", provider.ProviderName);
        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void WlrScreencopyProvider_WhenSupportIsProvided_DoesNotProbeCaptureBackend()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Failure(
            ScreenReadErrorKind.BackendUnavailable,
            "capture probe should not run"));

        using var provider = new WlrScreencopyScreenFrameProvider(capture, WlrScreencopySupportResult.Supported());

        Assert.True(provider.IsSupported);
        Assert.Equal(0, capture.ProbeCalls);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenProtocolUnsupported_ReturnsUnavailableAndDoesNotCapture()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Unsupported("wlr global missing"));

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("wlr global missing", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureSucceeds_ReturnsSharedScreenFrameWithNormalizedPixels()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.WlrFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var capture = new FakeWlrScreencopyCapture(
            WlrScreencopySupportResult.Supported(),
            WlrScreencopyCaptureResult.Success(frame));

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(10, 20, 2, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(10, 20, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), resultFrame.GetPixel(new ScreenPoint(10, 20)));
        Assert.Equal(new ScreenPixelColor(0x44, 0x55, 0x66), resultFrame.GetPixel(new ScreenPoint(11, 20)));
        Assert.Equal(new ScreenRect(10, 20, 2, 1), capture.LastRegion);
        Assert.Equal(0, owner.DisposeCount);

        resultFrame.Dispose();
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureFails_ReturnsStructuredFailure()
    {
        var capture = new FakeWlrScreencopyCapture(
            WlrScreencopySupportResult.Supported(),
            WlrScreencopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "copy failed"));

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("copy failed", result.ErrorMessage);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Supported());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureThrowsCancellation_ReturnsStructuredCanceledFailure()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Supported())
        {
            CaptureException = new OperationCanceledException("capture canceled")
        };

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureThrowsTimeout_ReturnsStructuredTimeoutFailure()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Supported())
        {
            CaptureException = new TimeoutException("capture timed out")
        };

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("capture timed out", result.ErrorMessage);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public void WlrScreencopyProvider_WhenDisposed_DisposesCaptureBackendOnce()
    {
        var capture = new FakeWlrScreencopyCapture(WlrScreencopySupportResult.Supported());
        var provider = new WlrScreencopyScreenFrameProvider(capture);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, capture.DisposeCount);
    }
}
