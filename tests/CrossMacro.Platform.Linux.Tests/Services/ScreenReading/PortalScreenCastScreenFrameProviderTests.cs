using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastScreenFrameProviderTests
{
    [Fact]
    public void PortalScreenCastProvider_WhenProbeIsSupported_ReportsSupported()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Supported());

        using var provider = new PortalScreenCastScreenFrameProvider(capture);

        Assert.Equal("XDG Desktop Portal ScreenCast", provider.ProviderName);
        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void PortalScreenCastProvider_WhenSupportIsProvided_DoesNotProbeCaptureBackend()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Failure(
            ScreenReadErrorKind.BackendUnavailable,
            "capture probe should not run"));

        using var provider = new PortalScreenCastScreenFrameProvider(capture, PortalScreenCastSupportResult.Supported());

        Assert.True(provider.IsSupported);
        Assert.Equal(0, capture.ProbeCalls);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenPortalUnsupported_ReturnsUnavailableAndDoesNotCapture()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Unsupported("portal missing"));

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("portal missing", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenCaptureSucceeds_ReturnsSharedScreenFrameWithNormalizedPixels()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var capture = new FakePortalScreenCastCapture(
            PortalScreenCastSupportResult.Supported(),
            PortalScreenCastCaptureResult.Success(frame));

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(10, 20, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), resultFrame.GetPixel(new ScreenPoint(10, 20)));
        Assert.Equal(new ScreenPixelColor(0x44, 0x55, 0x66), resultFrame.GetPixel(new ScreenPoint(11, 20)));
        Assert.Equal(0, owner.DisposeCount);

        resultFrame.Dispose();
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenPipeWireCaptureFails_ReturnsStructuredFailure()
    {
        var capture = new FakePortalScreenCastCapture(
            PortalScreenCastSupportResult.Supported(),
            PortalScreenCastCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "pipewire failed"));

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("pipewire failed", result.ErrorMessage);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Supported());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenCaptureThrowsCancellation_ReturnsStructuredCanceledFailure()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Supported())
        {
            CaptureException = new OperationCanceledException("capture canceled")
        };

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenCaptureThrowsTimeout_ReturnsStructuredTimeoutFailure()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Supported())
        {
            CaptureException = new TimeoutException("capture timed out")
        };

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("capture timed out", result.ErrorMessage);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenRegionOutsideFullFrame_ReturnsOutOfBoundsAndDisposesCaptureFrame()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.PortalFrame(new ScreenRect(0, 0, 2, 2), new byte[16], owner);
        var capture = new FakePortalScreenCastCapture(
            PortalScreenCastSupportResult.Supported(),
            PortalScreenCastCaptureResult.Success(frame));

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 1, 2, 2), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task PortalScreenCastProvider_WhenRegionInsideFullFrame_ReturnsMappedRegionAndDisposesCaptureFrameOnce()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.PortalFrame(
            new ScreenRect(0, 0, 3, 2),
            ScreenReadingFrameFixtures.ThreeByTwoXrgbBytes(),
            owner);
        var capture = new FakePortalScreenCastCapture(
            PortalScreenCastSupportResult.Supported(),
            PortalScreenCastCaptureResult.Success(frame));

        using var provider = new PortalScreenCastScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 0, 2, 2), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ScreenRect(1, 0, 2, 2), capture.LastRegion);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(1, 0, 2, 2), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x04, 0x05, 0x06), resultFrame.GetPixel(new ScreenPoint(1, 0)));
        Assert.Equal(new ScreenPixelColor(0x10, 0x11, 0x12), resultFrame.GetPixel(new ScreenPoint(2, 1)));
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public void PortalScreenCastProvider_WhenDisposed_DisposesCaptureBackendOnce()
    {
        var capture = new FakePortalScreenCastCapture(PortalScreenCastSupportResult.Supported());
        var provider = new PortalScreenCastScreenFrameProvider(capture);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, capture.DisposeCount);
    }
}
