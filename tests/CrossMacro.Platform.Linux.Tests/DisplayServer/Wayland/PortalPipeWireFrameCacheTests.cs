namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireFrameCacheTests
{
    [Fact]
    public void TryCreateFrame_WhenNoFrameIsCached_ReturnsFalse()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);

        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out _));
    }

    [Fact]
    public void FullUpdate_AllowsSubregionReads()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);
        Store(cache, generation: 1, [1, 2, 3, 0, 4, 5, 6, 0]);

        Assert.True(cache.TryCreateFrame(new ScreenRect(1, 0, 1, 1), out var frame));
        using (frame)
        {
            Assert.Equal([4, 5, 6, 0], frame!.Pixels.ToArray());
            Assert.Equal(new ScreenRect(0, 0, 1, 1), frame.LogicalBounds);
        }
    }

    [Fact]
    public void NewerGeneration_ReplacesEveryPixelOfThePreviousFrame()
    {
        var cache = new PortalPipeWireFrameCache(2, 1);
        Store(cache, generation: 1, [1, 1, 1, 0, 2, 2, 2, 0]);
        Store(cache, generation: 2, [9, 9, 9, 0, 8, 8, 8, 0]);

        Assert.True(cache.TryCreateFrame(new ScreenRect(0, 0, 2, 1), out var frame));
        using (frame)
        {
            Assert.Equal([9, 9, 9, 0, 8, 8, 8, 0], frame!.Pixels.ToArray());
        }
    }

    [Fact]
    public void OlderGenerationCannotOverwriteNewerPixels()
    {
        var cache = new PortalPipeWireFrameCache(1, 1);
        Store(cache, generation: 2, [9, 8, 7, 0]);

        using (var stale = cache.BeginFullUpdate(generation: 1))
        {
            Assert.False(stale.IsAccepted);
        }

        Assert.True(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out var frame));
        using (frame)
        {
            Assert.Equal([9, 8, 7, 0], frame!.Pixels.ToArray());
        }
    }

    [Fact]
    public void AbandonedUpdate_DoesNotExposeTheHalfWrittenImage()
    {
        var cache = new PortalPipeWireFrameCache(1, 1);
        Store(cache, generation: 1, [1, 2, 3, 0]);

        using (var update = cache.BeginFullUpdate(generation: 2))
        {
            Assert.True(update.IsAccepted);
            update.Pixels[0] = 99;
        }

        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out _));
    }

    [Fact]
    public void Clear_RemovesPreviouslyCachedFrame()
    {
        var cache = new PortalPipeWireFrameCache(1, 1);
        Store(cache, generation: 1, [1, 2, 3, 0]);

        cache.Clear();

        Assert.False(cache.TryCreateFrame(new ScreenRect(0, 0, 1, 1), out _));
    }

    private static void Store(PortalPipeWireFrameCache cache, long generation, byte[] pixels)
    {
        using var update = cache.BeginFullUpdate(generation);
        Assert.True(update.IsAccepted);
        pixels.AsSpan().CopyTo(update.Pixels);
        update.Commit();
    }
}
