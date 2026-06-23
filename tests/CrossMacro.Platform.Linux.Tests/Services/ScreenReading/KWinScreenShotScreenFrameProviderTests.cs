using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class KWinScreenShotScreenFrameProviderTests
{
    [Fact]
    public void KWinProvider_WhenProbeIsSupported_ReportsSupported()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());

        using var provider = new KWinScreenShotScreenFrameProvider(capture);

        Assert.Equal("KDE KWin ScreenShot2", provider.ProviderName);
        Assert.True(provider.IsSupported);
    }

    [Fact]
    public void KWinProvider_WhenSupportIsProvided_DoesNotProbeCaptureBackend()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Failure(
            ScreenReadErrorKind.BackendUnavailable,
            "capture probe should not run"));

        using var provider = new KWinScreenShotScreenFrameProvider(capture, KWinScreenShotSupportResult.Supported());

        Assert.True(provider.IsSupported);
        Assert.Equal(0, capture.ProbeCalls);
    }

    [Fact]
    public async Task KWinProvider_WhenPermissionDenied_ReturnsUnavailableAndDoesNotCapture()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Failure(
            ScreenReadErrorKind.PermissionDenied,
            "desktop permission missing"));

        using var provider = new KWinScreenShotScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 2, 1, 1), ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("desktop permission missing", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task KWinProvider_WhenCaptureSucceeds_ReturnsRegionFrame()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.KWinFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var capture = new FakeKWinScreenShotCapture(
            KWinScreenShotSupportResult.Supported(),
            KWinScreenShotCaptureResult.Success(frame));

        using var provider = new KWinScreenShotScreenFrameProvider(capture);
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
    public async Task KWinProvider_WhenNullRegion_ReturnsUnsupportedWithoutCapture()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());

        using var provider = new KWinScreenShotScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, result.ErrorKind);
        Assert.Contains("bounded region", result.ErrorMessage);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task KWinProvider_WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var provider = new KWinScreenShotScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 2, 1, 1), new ScreenReadOptions(cancellationToken: cts.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, capture.CaptureCalls);
    }

    [Fact]
    public async Task KWinProvider_WhenCaptureThrowsCancellation_ReturnsStructuredCanceledFailure()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported())
        {
            CaptureException = new OperationCanceledException("capture canceled")
        };

        using var provider = new KWinScreenShotScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 2, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public void KWinProvider_WhenDisposed_DisposesCaptureBackendOnce()
    {
        var capture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());
        var provider = new KWinScreenShotScreenFrameProvider(capture);

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(1, capture.DisposeCount);
    }
}
