namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandGlobalCatalogTests
{
    [Theory]
    [InlineData("wl_output", 4u, "Output", 4u)]
    [InlineData("wl_output", 2u, "Output", 2u)]
    [InlineData("wl_seat", 8u, "Seat", 1u)]
    [InlineData("wl_shm", 2u, "Shm", 1u)]
    [InlineData("zxdg_output_manager_v1", 3u, "XdgOutputManager", 3u)]
    [InlineData("zxdg_output_manager_v1", 1u, "XdgOutputManager", 1u)]
    [InlineData("ext_output_image_capture_source_manager_v1", 1u, "ExtOutputSourceManager", 1u)]
    [InlineData("ext_image_copy_capture_manager_v1", 1u, "ExtCopyManager", 1u)]
    [InlineData("zwlr_screencopy_manager_v1", 3u, "WlrScreencopyManager", 3u)]
    [InlineData("zwlr_screencopy_manager_v1", 4u, "WlrScreencopyManager", 3u)]
    public void Resolve_BindsKnownGlobalsAtTheHighestSupportedVersion(string iface, uint advertised, string expectedSlot, uint expectedVersion)
    {
        var match = WaylandGlobalCatalog.Resolve(iface, advertised, out var slot, out var bindVersion);

        Assert.Equal(WaylandGlobalMatch.Bindable, match);
        Assert.Equal(expectedSlot, slot.ToString());
        Assert.Equal(expectedVersion, bindVersion);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public void Resolve_RejectsWlrScreencopyVersionsThatNeverSendBufferDone(uint advertised)
    {
        var match = WaylandGlobalCatalog.Resolve("zwlr_screencopy_manager_v1", advertised, out _, out var bindVersion);

        Assert.Equal(WaylandGlobalMatch.VersionTooOld, match);
        Assert.Equal(0u, bindVersion);
    }

    [Theory]
    [InlineData("wl_compositor")]
    [InlineData("xdg_wm_base")]
    [InlineData("")]
    public void Resolve_IgnoresInterfacesTheClientDoesNotUse(string iface)
    {
        Assert.Equal(WaylandGlobalMatch.Unknown, WaylandGlobalCatalog.Resolve(iface, 1, out _, out _));
    }

    [Fact]
    public void TryReleaseSingleton_ReturnsTheSlotTheWithdrawnGlobalFilled()
    {
        var catalog = new WaylandGlobalCatalog();
        catalog.RecordSingleton(7, WaylandGlobalSlot.ExtCopyManager);
        catalog.RecordSingleton(9, WaylandGlobalSlot.Shm);

        Assert.True(catalog.TryReleaseSingleton(7, out var slot));
        Assert.Equal(WaylandGlobalSlot.ExtCopyManager, slot);
        Assert.False(catalog.TryReleaseSingleton(7, out _));
        Assert.True(catalog.TryReleaseSingleton(9, out slot));
        Assert.Equal(WaylandGlobalSlot.Shm, slot);
    }

    [Fact]
    public void TryReleaseSingleton_IgnoresGlobalsThatFilledNoSlot()
    {
        var catalog = new WaylandGlobalCatalog();
        catalog.RecordSingleton(3, WaylandGlobalSlot.Seat);

        Assert.False(catalog.TryReleaseSingleton(4, out _));
    }
}
