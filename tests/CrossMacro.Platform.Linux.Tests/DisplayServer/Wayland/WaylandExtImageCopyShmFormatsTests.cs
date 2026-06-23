using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandExtImageCopyShmFormatsTests
{
    [Theory]
    [InlineData(WaylandExtImageCopyShmFormats.Argb8888, ScreenPixelFormat.Bgra8888)]
    [InlineData(WaylandExtImageCopyShmFormats.Xrgb8888, ScreenPixelFormat.Xrgb8888)]
    [InlineData(WaylandExtImageCopyShmFormats.Abgr8888, ScreenPixelFormat.Abgr8888)]
    [InlineData(WaylandExtImageCopyShmFormats.Xbgr8888, ScreenPixelFormat.Xbgr8888)]
    public void TryMap_MapsSupportedCodes(uint shmFormat, ScreenPixelFormat expectedPixelFormat)
    {
        var mapped = WaylandExtImageCopyShmFormats.TryMap(shmFormat, out var pixelFormat);

        Assert.True(mapped);
        Assert.Equal(expectedPixelFormat, pixelFormat);
    }

    [Fact]
    public void TrySelectPreferredPixelFormat_PrefersXrgbOverArgb()
    {
        var selected = WaylandExtImageCopyShmFormats.TrySelectPreferredPixelFormat(
            new[] { WaylandExtImageCopyShmFormats.Argb8888, WaylandExtImageCopyShmFormats.Xrgb8888 },
            out var pixelFormat);

        Assert.True(selected);
        Assert.Equal(ScreenPixelFormat.Xrgb8888, pixelFormat);
    }

    [Fact]
    public void TrySelectPreferredShmFormat_PrefersXrgbOverArgb()
    {
        var selected = WaylandExtImageCopyShmFormats.TrySelectPreferredShmFormat(
            new[] { WaylandExtImageCopyShmFormats.Argb8888, WaylandExtImageCopyShmFormats.Xrgb8888 },
            out var shmFormat);

        Assert.True(selected);
        Assert.Equal(WaylandExtImageCopyShmFormats.Xrgb8888, shmFormat);
    }

    [Fact]
    public void TrySelectPreferredPixelFormat_PrefersAbgrOverXbgr()
    {
        var selected = WaylandExtImageCopyShmFormats.TrySelectPreferredPixelFormat(
            new[] { WaylandExtImageCopyShmFormats.Xbgr8888, WaylandExtImageCopyShmFormats.Abgr8888 },
            out var pixelFormat);

        Assert.True(selected);
        Assert.Equal(ScreenPixelFormat.Abgr8888, pixelFormat);
    }

    [Fact]
    public void TrySelectPreferredShmFormat_PrefersAbgrOverXbgr()
    {
        var selected = WaylandExtImageCopyShmFormats.TrySelectPreferredShmFormat(
            new[] { WaylandExtImageCopyShmFormats.Xbgr8888, WaylandExtImageCopyShmFormats.Abgr8888 },
            out var shmFormat);

        Assert.True(selected);
        Assert.Equal(WaylandExtImageCopyShmFormats.Abgr8888, shmFormat);
    }

    [Fact]
    public void TrySelectPreferredShmFormat_IgnoresUnsupportedFormats()
    {
        var selected = WaylandExtImageCopyShmFormats.TrySelectPreferredShmFormat(
            new[] { 0x12345678U, WaylandExtImageCopyShmFormats.Xbgr8888 },
            out var shmFormat);

        Assert.True(selected);
        Assert.Equal(WaylandExtImageCopyShmFormats.Xbgr8888, shmFormat);
    }

    [Fact]
    public void FormatAdvertisedFormats_UsesStableLowercaseHex()
    {
        var formatted = WaylandExtImageCopyShmFormats.FormatAdvertisedFormats(
            new[] { WaylandExtImageCopyShmFormats.Xbgr8888, WaylandExtImageCopyShmFormats.Abgr8888 });

        Assert.Equal("[0x34324258,0x34324241]", formatted);
    }
}
