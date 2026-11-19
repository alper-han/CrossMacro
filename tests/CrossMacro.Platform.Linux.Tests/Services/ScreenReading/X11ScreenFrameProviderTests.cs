
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class X11ScreenFrameProviderTests
{



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



}
