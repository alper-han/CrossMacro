
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class WlrScreencopyScreenFrameProviderTests
{



    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureSucceeds_ReturnsSharedScreenFrameWithNormalizedPixels()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.WlrFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner);
        var capture = new FakeWlrScreencopyCapture(
            WaylandSupportResult.Supported(),
            WaylandCaptureResult.Success(frame));

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
    public async Task WlrScreencopyProvider_WhenCaptureIncludesValidPixelMask_PreservesMaskInScreenFrame()
    {
        var owner = new CountingDisposable();
        var frame = ScreenReadingFrameFixtures.WlrFrame(
            new ScreenRect(10, 20, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes(),
            owner,
            new byte[] { 1, 0 });
        var capture = new FakeWlrScreencopyCapture(
            WaylandSupportResult.Supported(),
            WaylandCaptureResult.Success(frame));

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(new ScreenRect(10, 20, 2, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.True(resultFrame.TryGetPixel(new ScreenPoint(10, 20), out var color));
        Assert.Equal(new ScreenPixelColor(0x11, 0x22, 0x33), color);
        Assert.False(resultFrame.TryGetPixel(new ScreenPoint(11, 20), out _));
        Assert.Equal(0, owner.DisposeCount);

        resultFrame.Dispose();
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenCaptureFails_ReturnsStructuredFailure()
    {
        var capture = new FakeWlrScreencopyCapture(
            WaylandSupportResult.Supported(),
            WaylandCaptureResult.Failure(ScreenReadErrorKind.CaptureFailed, "copy failed"));

        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        var result = await provider.CaptureFrameAsync(region: null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("copy failed", result.ErrorMessage, StringComparison.Ordinal);
    }




    [Fact]
    public async Task WlrScreencopyProvider_WhenCanceledDuringNativeSession_ReturnsCanceled()
    {
        var capture = new FakeWlrScreencopyCapture(WaylandSupportResult.Supported())
        {
            DelayBeforeResult = TimeSpan.FromSeconds(5),
        };
        using var provider = new WlrScreencopyScreenFrameProvider(capture);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = provider.CaptureFrameAsync(region: null, new ScreenReadOptions(cancellationToken: cts.Token));

        await capture.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        var result = await pending;

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

    [Fact]
    public async Task WlrScreencopyProvider_WhenNativeSessionExceedsTimeout_ReturnsTimeout()
    {
        var capture = new FakeWlrScreencopyCapture(WaylandSupportResult.Supported())
        {
            DelayBeforeResult = TimeSpan.FromSeconds(5),
        };
        using var provider = new WlrScreencopyScreenFrameProvider(capture);

        var result = await provider.CaptureFrameAsync(region: null, new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Equal(1, capture.CaptureCalls);
    }

}
