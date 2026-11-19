
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class KWinScreenShotScreenFrameProviderTests
{


    [Fact]
    public async Task KWinProvider_WhenPermissionDenied_ReturnsUnavailableAndDoesNotCapture()
    {
        var support = WaylandSupportResult.Failure(
            ScreenReadErrorKind.PermissionDenied,
            "desktop permission missing");
        var capture = new FakeKWinScreenShotCapture(support);

        using var provider = new KWinScreenShotScreenFrameProvider(capture, support);
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 2, 1, 1), ScreenReadOptions.Default);

        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("desktop permission missing", result.ErrorMessage, StringComparison.Ordinal);
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
            WaylandSupportResult.Supported(),
            WaylandCaptureResult.Success(frame));

        using var provider = new KWinScreenShotScreenFrameProvider(capture, WaylandSupportResult.Supported());
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
    public async Task KWinProvider_WhenNullRegion_UsesWorkspaceCapture()
    {
        var frame = ScreenReadingFrameFixtures.KWinFrame(
            new ScreenRect(0, 0, 2, 1),
            ScreenReadingFrameFixtures.TwoPixelXrgbBytes());
        var capture = new FakeKWinScreenShotCapture(
            WaylandSupportResult.Supported(),
            WaylandCaptureResult.Success(frame));

        using var provider = new KWinScreenShotScreenFrameProvider(capture, WaylandSupportResult.Supported());
        var result = await provider.CaptureFrameAsync(region: null, ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        using var resultFrame = Assert.IsType<ScreenFrame>(result.Value);
        Assert.Equal(new ScreenRect(0, 0, 2, 1), resultFrame.LogicalBounds);
        Assert.Equal(0, capture.CaptureCalls);
        Assert.Equal(1, capture.WorkspaceCaptureCalls);
    }



}
