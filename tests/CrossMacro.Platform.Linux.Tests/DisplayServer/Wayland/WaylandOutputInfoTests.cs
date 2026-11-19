namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandOutputInfoTests
{
    [Theory]
    [InlineData(3840, 2160, 2, 0, 1920, 1080)]
    [InlineData(1920, 1080, 1, 1, 1080, 1920)]
    [InlineData(3000, 2000, 2, 5, 1000, 1500)]
    [InlineData(2560, 1440, 0, 0, 2560, 1440)]
    public void ResolveFallbackLogicalSize_AppliesIntegerScaleAndOutputTransform(
        int modeWidth,
        int modeHeight,
        int scale,
        int transform,
        int expectedWidth,
        int expectedHeight)
    {
        var size = WaylandOutputInfo.ResolveFallbackLogicalSize(
            modeWidth,
            modeHeight,
            scale,
            transform);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }

    [Theory]
    [InlineData(0, 1080, 1, 0)]
    [InlineData(-1920, 1080, 1, 0)]
    [InlineData(1920, 0, 1, 0)]
    [InlineData(1920, 1080, 1, 0)]
    public void ResolveFallbackLogicalSize_ReturnsZeroForNonPositiveModeDimensions(
        int modeWidth,
        int modeHeight,
        int scale,
        int transform)
    {
        var size = WaylandOutputInfo.ResolveFallbackLogicalSize(
            modeWidth,
            modeHeight,
            scale,
            transform);

        if (modeWidth > 0 && modeHeight > 0)
        {
            Assert.NotEqual((0, 0), size);
        }
        else
        {
            Assert.Equal((0, 0), size);
        }
    }

    [Fact]
    public void ResolveFallbackLogicalSize_UsesCeilingAfterAxisSwapAndInvalidScaleFallback()
    {
        var size = WaylandOutputInfo.ResolveFallbackLogicalSize(
            modeWidth: 3001,
            modeHeight: 2001,
            scale: 0,
            transform: 3);

        Assert.Equal((2001, 3001), size);
    }

    [Fact]
    public void Events_UpdateGeometryModeAndScale_AndAdvanceTheGenerationOnlyOnChange()
    {
        using var output = new WaylandOutputInfo(globalName: 9, new IntPtr(1));
        using var geometry = new WlArgumentPack(8);
        geometry[0] = new WlArgument { i = -1920 };
        geometry[1] = new WlArgument { i = 40 };
        geometry[7] = new WlArgument { i = 0 };
        using var mode = new WlArgumentPack(5);
        mode[0] = new WlArgument { u = 1 };
        mode[1] = new WlArgument { i = 3840 };
        mode[2] = new WlArgument { i = 2160 };
        using var scale = new WlArgumentPack(1);
        scale[0] = new WlArgument { i = 2 };

        _ = WaylandDispatcherInvoker.Invoke(output.DispatcherPtr, 0, geometry);
        _ = WaylandDispatcherInvoker.Invoke(output.DispatcherPtr, 1, mode);
        _ = WaylandDispatcherInvoker.Invoke(output.DispatcherPtr, 3, scale);

        Assert.Equal((-1920, 40, 1920, 1080), (output.X, output.Y, output.ModeWidth, output.ModeHeight));
        var generation = output.Generation;

        _ = WaylandDispatcherInvoker.Invoke(output.DispatcherPtr, 3, scale);

        Assert.Equal(generation, output.Generation);
    }
}
