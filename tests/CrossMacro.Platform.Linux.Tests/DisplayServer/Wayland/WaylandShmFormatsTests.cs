
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandShmFormatsTests
{
    [Theory]
    [InlineData(WaylandShmFormats.Argb8888, ScreenPixelFormat.Bgra8888)]
    [InlineData(WaylandShmFormats.Xrgb8888, ScreenPixelFormat.Xrgb8888)]
    [InlineData(WaylandShmFormats.Rgb888, ScreenPixelFormat.Bgr24)]
    [InlineData(WaylandShmFormats.Bgr888, ScreenPixelFormat.Rgb24)]
    [InlineData(WaylandShmFormats.Abgr8888, ScreenPixelFormat.Abgr8888)]
    [InlineData(WaylandShmFormats.Xbgr8888, ScreenPixelFormat.Xbgr8888)]
    public void TryMap_MapsSupportedCodes(uint shmFormat, ScreenPixelFormat expectedPixelFormat)
    {
        var mapped = WaylandShmFormats.TryMap(shmFormat, out var pixelFormat);

        Assert.True(mapped);
        Assert.Equal(expectedPixelFormat, pixelFormat);
    }

    [Theory]
    [InlineData(WaylandShmFormats.Rgb888, 7, 21)]
    [InlineData(WaylandShmFormats.Bgr888, 7, 21)]
    [InlineData(WaylandShmFormats.Xrgb8888, 7, 28)]
    public void TryGetStride_UsesMappedBytesPerPixel(uint shmFormat, uint width, int expectedStride)
    {
        var valid = WaylandShmFormats.TryGetStride(shmFormat, width, out var stride);

        Assert.True(valid);
        Assert.Equal(expectedStride, stride);
    }

    [Fact]
    public void TryGetStride_RejectsUnknownFormat()
    {
        var valid = WaylandShmFormats.TryGetStride(0x12345678U, 7, out var stride);

        Assert.False(valid);
        Assert.Equal(0, stride);
    }

    [Fact]
    public void TryGetStride_RejectsWidthThatWouldOverflowInt32()
    {
        var width = ((uint)int.MaxValue / 4U) + 1U;

        var valid = WaylandShmFormats.TryGetStride(WaylandShmFormats.Xrgb8888, width, out var stride);

        Assert.False(valid);
        Assert.Equal(0, stride);
    }

    [Fact]
    public void ShouldReplaceSelectedFormat_PrefersXrgbOverArgb()
    {
        Assert.True(WaylandShmFormats.ShouldReplaceSelectedFormat(
            WaylandShmFormats.Xrgb8888,
            WaylandShmFormats.Argb8888));
    }

    [Fact]
    public void ShouldReplaceSelectedFormat_PrefersAbgrOverXbgr()
    {
        Assert.True(WaylandShmFormats.ShouldReplaceSelectedFormat(
            WaylandShmFormats.Abgr8888,
            WaylandShmFormats.Xbgr8888));
    }

    [Fact]
    public void ShouldReplaceSelectedFormat_DoesNotReplacePreferredOrInvalidSelections()
    {
        Assert.False(WaylandShmFormats.ShouldReplaceSelectedFormat(
            WaylandShmFormats.Argb8888,
            WaylandShmFormats.Xrgb8888));
        Assert.False(WaylandShmFormats.ShouldReplaceSelectedFormat(
            0x12345678U,
            WaylandShmFormats.Argb8888));
        Assert.False(WaylandShmFormats.ShouldReplaceSelectedFormat(
            WaylandShmFormats.Xrgb8888,
            0x12345678U));
    }

    [Fact]
    public void FormatAdvertisedFormats_UsesStableLowercaseHex()
    {
        var formatted = WaylandShmFormats.FormatAdvertisedFormats(
            [WaylandShmFormats.Xbgr8888, WaylandShmFormats.Abgr8888]);

        Assert.Equal("[0x34324258,0x34324241]", formatted);
    }

    [Fact]
    public void FormatAdvertisedFormats_FormatsEmptyInputAsEmptyArray()
    {
        Assert.Equal("[]", WaylandShmFormats.FormatAdvertisedFormats([]));
    }

    [Fact]
    public void CaptureCancellation_WhenTokenIsCanceled_ThrowsBeforeNativePolling()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = new WaylandCaptureCancellation(new ScreenReadOptions(cancellationToken: cancellation.Token));

        _ = Assert.Throws<OperationCanceledException>(state.ThrowIfCancellationRequested);
    }

    [Fact]
    public void CaptureCancellation_WhenDeadlineExpires_ThrowsTimeout()
    {
        var state = new WaylandCaptureCancellation(new ScreenReadOptions(timeout: TimeSpan.Zero));

        _ = Assert.Throws<TimeoutException>(state.ThrowIfCancellationRequested);
    }

    [Fact]
    public void CaptureCancellation_WithMaximumTimeout_SaturatesPollDeadline()
    {
        var state = new WaylandCaptureCancellation(new ScreenReadOptions(timeout: TimeSpan.MaxValue));

        Assert.Equal(100, state.GetPollTimeoutMilliseconds());
    }
}
