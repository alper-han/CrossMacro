using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class ExtImageCopyScreenFrameProviderTests
{
    [Fact]
    public void ExtImageCopyProvider_WhenProbeIsSupported_ReportsSupported()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported());

        using var provider = new ExtImageCopyScreenFrameProvider(capture);

        Assert.Equal("Wayland ext-image-copy-capture-v1", provider.ProviderName);
        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void ExtImageCopyProvider_WhenSupportIsProvided_DoesNotProbeCaptureBackend()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Failure(
            ScreenReadErrorKind.BackendUnavailable,
            "capture probe should not run"));

        using var provider = new ExtImageCopyScreenFrameProvider(capture, ExtImageCopySupportResult.Supported());

        Assert.True(provider.IsSupported);
        Assert.Equal(0, capture.ProbeCalls);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenProtocolUnsupported_ReportsUnavailableAndDoesNotCapture()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Unsupported("protocol missing"));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("protocol missing", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCaptureSucceeds_ReturnsSharedScreenFrameWithNormalizedPixels()
    {
        var frame = ScreenReadingFrameFixtures.ExtFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            new CountingDisposable());
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported(), ExtImageCopyCaptureResult.Success(frame));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        using var resultFrame = (await provider.CaptureFrameAsync(null, ScreenReadOptions.Default)).Value;

        Assert.NotNull(resultFrame);
        Assert.Equal(new ScreenRect(10, 20, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), resultFrame.GetPixel(new ScreenPoint(10, 20)));
        Assert.Equal(new ScreenPixelColor(0x44, 0x55, 0x66), resultFrame.GetPixel(new ScreenPoint(11, 20)));
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCapturedTwice_ReturnsBothFrames()
    {
        var firstFrame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 1, 1), [0x30, 0x20, 0x10, 0x00]);
        var secondFrame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 1, 1), [0x60, 0x50, 0x40, 0x00]);
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported(), ExtImageCopyCaptureResult.Success(firstFrame));
        capture.EnqueueCaptureResult(ExtImageCopyCaptureResult.Success(secondFrame));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        using var firstResultFrame = (await provider.CaptureFrameAsync(null, ScreenReadOptions.Default)).Value;
        using var secondResultFrame = (await provider.CaptureFrameAsync(null, ScreenReadOptions.Default)).Value;

        Assert.Equal(2, capture.CaptureCalls);
        Assert.Equal(new ScreenPixelColor(0x10, 0x20, 0x30), firstResultFrame!.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(0x40, 0x50, 0x60), secondResultFrame!.GetPixel(new ScreenPoint(0, 0)));
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCaptureTimesOut_ReturnsStructuredTimeoutFailure()
    {
        var capture = new FakeExtImageCopyCapture(
            ExtImageCopySupportResult.Supported(),
            ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureTimeout, "frame timed out"));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("frame timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCaptureThrowsCancellation_ReturnsStructuredCanceledFailure()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported())
        {
            CaptureException = new OperationCanceledException("capture canceled")
        };

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenCaptureThrowsTimeout_ReturnsStructuredTimeoutFailure()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported())
        {
            CaptureException = new TimeoutException("capture timed out")
        };

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("capture timed out", result.ErrorMessage);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenRegionOutsideFullFrame_ReturnsOutOfBoundsAndDisposesCaptureFrame()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 2, 2), new byte[16], owner);
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported(), ExtImageCopyCaptureResult.Success(frame));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 1, 2, 2), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task ExtImageCopyProvider_WhenRegionInsideFullFrame_ReturnsMappedRegionAndDisposesCaptureFrameOnce()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.ExtFrame(
            new ScreenRect(0, 0, 3, 2),
            ScreenReadingFrameFixtures.ThreeByTwoXrgbBytes(),
            owner);
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported(), ExtImageCopyCaptureResult.Success(frame));

        using var provider = new ExtImageCopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 0, 2, 2), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(1, 0, 2, 2), resultFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(0x04, 0x05, 0x06), resultFrame.GetPixel(new ScreenPoint(1, 0)));
        Assert.Equal(new ScreenPixelColor(0x10, 0x11, 0x12), resultFrame.GetPixel(new ScreenPoint(2, 1)));
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public void ExtImageCopyProvider_WhenDisposed_DisposesCaptureBackendOnce()
    {
        var capture = new FakeExtImageCopyCapture(ExtImageCopySupportResult.Supported());
        var provider = new ExtImageCopyScreenFrameProvider(capture);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, capture.DisposeCount);
    }

    [Fact]
    public async Task ExtImageCopyCapture_WhenSupported_DelegatesToNativeSessionFactory()
    {
        var frame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 1, 1), [0x30, 0x20, 0x10, 0x00]);
        var factory = new FakeExtImageCopyNativeCaptureSessionFactory(ExtImageCopyCaptureResult.Success(frame));
        using var capture = new ExtImageCopyCapture(new FakeExtImageCopyProbe(ExtImageCopySupportResult.Supported()), factory);

        var result = await capture.CaptureAsync(new ScreenRect(10, 20, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(frame, result.Frame);
        Assert.Equal(1, factory.CaptureCalls);
        Assert.Equal(new ScreenRect(10, 20, 1, 1), factory.LastRegion);
    }

    [Fact]
    public async Task ExtImageCopyCapture_WhenUnsupported_DoesNotCreateNativeSession()
    {
        var factory = new FakeExtImageCopyNativeCaptureSessionFactory(
            ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "should not be used"));
        using var capture = new ExtImageCopyCapture(new FakeExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("missing ext global")), factory);

        var result = await capture.CaptureAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Equal(0, factory.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyCaptureSupported_WhenProbeUnsupported_DelegatesToNativeSessionFactory()
    {
        var frame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 1, 1), [0x30, 0x20, 0x10, 0x00]);
        var factory = new FakeExtImageCopyNativeCaptureSessionFactory(ExtImageCopyCaptureResult.Success(frame));
        using var capture = new ExtImageCopyCapture(new FakeExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("probe already handled")), factory);

        var result = await capture.CaptureSupportedAsync(null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(frame, result.Frame);
        Assert.Equal(1, factory.CaptureCalls);
    }

    [Fact]
    public async Task ExtImageCopyCapture_WhenNativeSessionCompletesAfterTimeout_ReturnsSessionResult()
    {
        var frame = ScreenReadingFrameFixtures.ExtFrame(new ScreenRect(0, 0, 1, 1), [0x30, 0x20, 0x10, 0x00]);
        var factory = new FakeExtImageCopyNativeCaptureSessionFactory(ExtImageCopyCaptureResult.Success(frame))
        {
            DelayBeforeResult = TimeSpan.FromMilliseconds(25)
        };
        using var capture = new ExtImageCopyCapture(new FakeExtImageCopyProbe(ExtImageCopySupportResult.Supported()), factory);

        var result = await capture.CaptureAsync(null, new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        Assert.True(result.IsSuccess);
        Assert.Same(frame, result.Frame);
        Assert.Equal(1, factory.CaptureCalls);
    }

    [Fact]
    public void ExtImageCopyCapture_WhenDisposed_DisposesNativeSessionFactory()
    {
        var factory = new FakeExtImageCopyNativeCaptureSessionFactory(
            ExtImageCopyCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "unused"));
        var capture = new ExtImageCopyCapture(new FakeExtImageCopyProbe(ExtImageCopySupportResult.Supported()), factory);

        capture.Dispose();
        capture.Dispose();

        Assert.Equal(1, factory.DisposeCount);
    }

}
