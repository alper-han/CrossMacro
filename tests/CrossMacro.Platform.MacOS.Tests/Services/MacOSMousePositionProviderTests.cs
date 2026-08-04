
namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSMousePositionProviderTests
{
    [Fact]
    public void ReadPosition_WhenEventRefIsZero_ReturnsNull()
    {
        var position = MacOSMousePositionProvider.ReadPosition(IntPtr.Zero);

        Assert.Null(position);
    }

    [Fact]
    public async Task DesktopBounds_WithMultipleDisplays_PreservesNegativeOriginAndUnion()
    {
        var native = new FakeCoreGraphicsNative(
            (1, CreateRect(-1920, -200, 1920, 1080)),
            (2, CreateRect(0, 0, 2560, 1440)));
        using var provider = new MacOSMousePositionProvider(native);

        var bounds = await provider.GetDesktopBoundsAsync();
        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
        Assert.Equal((4480, 1640), resolution);
    }

    private static CoreGraphics.CGRect CreateRect(double x, double y, double width, double height) => new()
    {
        origin = new CoreGraphics.CGPoint { X = x, Y = y },
        size = new CoreGraphics.CGSize { width = width, height = height },
    };

    private sealed class FakeCoreGraphicsNative(params (uint Display, CoreGraphics.CGRect Bounds)[] displays) :
        IMacOSCoreGraphicsNative
    {
        private readonly IReadOnlyDictionary<uint, CoreGraphics.CGRect> _displays =
            displays.ToDictionary(static item => item.Display, static item => item.Bounds);

        public uint GetActiveDisplayCount() => checked((uint)_displays.Count);

        public uint[] GetActiveDisplays(uint count) =>
            _displays.Keys.Take(checked((int)count)).ToArray();

        public uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect) => [];

        public CoreGraphics.CGRect GetDisplayBounds(uint display) => _displays[display];

        public MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect) =>
            throw new NotSupportedException();
    }
}
