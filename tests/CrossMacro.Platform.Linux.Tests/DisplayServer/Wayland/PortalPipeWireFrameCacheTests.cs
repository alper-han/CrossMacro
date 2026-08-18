namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireFrameCacheTests
{
    [Fact]
    public void TryCreateFrame_WhenRequestedPixelsAreNotCached_ReturnsFalse()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);

        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out _));
    }

    [Fact]
    public void UpdateFullFrame_AllowsSubregionReads()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);
        cache.Update(new ScreenRect(0, 0, 2, 1), [1, 2, 3, 0, 4, 5, 6, 0], 8, generation: 1);

        Assert.True(cache.TryCreateFrame(new ScreenRect(1, 0, 1, 1), out var frame));
        using (frame)
        {
            Assert.Equal([4, 5, 6, 0], frame!.Pixels.ToArray());
            Assert.Equal(new ScreenRect(0, 0, 1, 1), frame.LogicalBounds);
        }
    }

    [Fact]
    public void BeginFullUpdate_StoresReusableFrameWithoutASecondFullBuffer()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);
        using (var update = cache.BeginFullUpdate(generation: 1))
        {
            Assert.True(update.IsAccepted);
            var pixels = update.Pixels;
            var expected = new byte[] { 1, 2, 3, 0, 4, 5, 6, 0 };
            expected.AsSpan().CopyTo(pixels);
            update.Commit();
        }

        Assert.True(cache.TryCreateFrame(new ScreenRect(0, 0, 2, 1), out var frame));
        using (frame)
        {
            Assert.Equal([1, 2, 3, 0, 4, 5, 6, 0], frame!.Pixels.ToArray());
        }
    }

    [Fact]
    public void UpdatePartialFrame_DoesNotExposeUncoveredPixels()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);
        cache.Update(new ScreenRect(0, 0, 1, 1), [1, 2, 3, 0], 4, generation: 1);

        Assert.True(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out var covered));
        covered!.Dispose();
        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 2, 1), out _));
    }

    [Fact]
    public void OlderGenerationCannotOverwriteNewerPixels()
    {
        var cache = new PortalPipeWireFrameCache(1, 1);
        cache.Update(new ScreenRect(0, 0, 1, 1), [9, 8, 7, 0], 4, generation: 2);
        cache.Update(new ScreenRect(0, 0, 1, 1), [1, 2, 3, 0], 4, generation: 1);

        Assert.True(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out var frame));
        using (frame)
        {
            Assert.Equal([9, 8, 7, 0], frame!.Pixels.ToArray());
        }
    }

    [Fact]
    public void Clear_RemovesPreviouslyCachedFrame()
    {
        var cache = new PortalPipeWireFrameCache(1, 1);
        cache.Update(new ScreenRect(0, 0, 1, 1), [1, 2, 3, 0], 4, generation: 1);

        cache.Clear();

        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out _));
    }
}
