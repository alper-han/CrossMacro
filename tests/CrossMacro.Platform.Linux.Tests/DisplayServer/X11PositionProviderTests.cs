namespace CrossMacro.Platform.Linux.Tests.DisplayServer;

public sealed class X11PositionProviderTests
{
    [Fact]
    public async Task GetAbsolutePositionAsync_WhenRootWindowIsNull_DoesNotQueryPointer()
    {
        var native = new FakeNative { Root = IntPtr.Zero };
        using var provider = new X11PositionProvider(native);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
        Assert.Equal(0, native.QueryPointerCalls);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_WhenPointerQuerySucceeds_ReturnsRootCoordinates()
    {
        var native = new FakeNative { PointerResult = (true, 120, 80) };
        using var provider = new X11PositionProvider(native);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((120, 80), position);
        Assert.Equal(1, native.QueryPointerCalls);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_WhenDimensionsAreInvalid_ReturnsNull()
    {
        var native = new FakeNative { Width = 0, Height = 1080 };
        using var provider = new X11PositionProvider(native);

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
        Assert.Equal(1, native.DisplayWidthCalls);
        Assert.Equal(1, native.DisplayHeightCalls);
    }

    [Fact]
    public async Task Dispose_IsIdempotentAndPreventsFurtherNativeCalls()
    {
        var native = new FakeNative();
        var provider = new X11PositionProvider(native);

        provider.Dispose();
        provider.Dispose();
        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
        Assert.Equal(1, native.CloseDisplayCalls);
        Assert.Equal(0, native.QueryPointerCalls);
    }

    private sealed class FakeNative : IX11PositionNativeApi
    {
        public IntPtr Display { get; } = new(1);

        public IntPtr Root { get; init; } = new(2);

        public (bool Success, int X, int Y) PointerResult { get; init; } = (true, 10, 20);

        public int Width { get; init; } = 1920;

        public int Height { get; init; } = 1080;

        public int QueryPointerCalls { get; private set; }

        public int DisplayWidthCalls { get; private set; }

        public int DisplayHeightCalls { get; private set; }

        public int CloseDisplayCalls { get; private set; }

        public IntPtr OpenDisplay(string? display) => Display;

        public int CloseDisplay(IntPtr display)
        {
            Assert.Equal(Display, display);
            CloseDisplayCalls++;
            return 0;
        }

        public IntPtr DefaultRootWindow(IntPtr display) => Root;

        public bool QueryPointer(IntPtr display, IntPtr window, out int rootX, out int rootY)
        {
            Assert.Equal(Display, display);
            Assert.Equal(Root, window);
            QueryPointerCalls++;
            rootX = PointerResult.X;
            rootY = PointerResult.Y;
            return PointerResult.Success;
        }

        public int DefaultScreen(IntPtr display) => 0;

        public int DisplayWidth(IntPtr display, int screen)
        {
            DisplayWidthCalls++;
            return Width;
        }

        public int DisplayHeight(IntPtr display, int screen)
        {
            DisplayHeightCalls++;
            return Height;
        }
    }
}
