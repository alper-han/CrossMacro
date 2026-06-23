using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.MacOS.Services.ScreenReading;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services.ScreenReading;

public sealed class CoreGraphicsMacOSScreenCaptureBackendTests
{
    private const CoreGraphics.CGBitmapInfo BgraBitmapInfo = CoreGraphics.CGBitmapInfo.ByteOrder32Little | CoreGraphics.CGBitmapInfo.AlphaPremultipliedFirst;

    [Fact]
    public void GetVirtualScreenBounds_UnionsMultipleDisplaysWithNegativeOrigins()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(-4, -2, 4, 4), SolidImage(4, 4, Pixel(1, 2, 3)))
            .AddDisplay(2, Rect(0, 0, 6, 3), SolidImage(6, 3, Pixel(4, 5, 6)));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var bounds = backend.GetVirtualScreenBounds();

        Assert.Equal(new ScreenRect(-4, -2, 10, 5), bounds);
    }

    [Fact]
    public void Capture_StitchesIntersectingDisplaysIntoRequestedRegion()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(-2, 0, 2, 1), Image(2, 1, 8, [Pixel(10, 0, 0), Pixel(20, 0, 0)]))
            .AddDisplay(2, Rect(0, 0, 2, 1), Image(2, 1, 8, [Pixel(30, 0, 0), Pixel(40, 0, 0)]));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var frame = backend.Capture(new ScreenRect(-2, 0, 4, 1), CancellationToken.None);

        using var screenFrame = new ScreenFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels);
        Assert.Equal(new ScreenPixelColor(10, 0, 0), screenFrame.GetPixel(new ScreenPoint(-2, 0)));
        Assert.Equal(new ScreenPixelColor(20, 0, 0), screenFrame.GetPixel(new ScreenPoint(-1, 0)));
        Assert.Equal(new ScreenPixelColor(30, 0, 0), screenFrame.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(40, 0, 0), screenFrame.GetPixel(new ScreenPoint(1, 0)));
    }

    [Fact]
    public void Capture_RespectsSourceBytesPerRowPadding()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(0, 0, 2, 2), Image(2, 2, 12, [Pixel(1, 0, 0), Pixel(2, 0, 0), Pixel(3, 0, 0), Pixel(4, 0, 0)]));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var frame = backend.Capture(new ScreenRect(0, 0, 2, 2), CancellationToken.None);

        using var screenFrame = new ScreenFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels);
        Assert.Equal(new ScreenPixelColor(3, 0, 0), screenFrame.GetPixel(new ScreenPoint(0, 1)));
        Assert.Equal(new ScreenPixelColor(4, 0, 0), screenFrame.GetPixel(new ScreenPoint(1, 1)));
    }

    [Fact]
    public void Capture_NormalizesRetinaBackingPixelsToLogicalDimensionsWithCenterSampling()
    {
        var pixels = new byte[4 * 2 * 4];
        WritePixel(pixels, 4, 1, 1, Pixel(11, 0, 0));
        WritePixel(pixels, 4, 3, 1, Pixel(33, 0, 0));
        var image = new MacOSCapturedImage(4, 2, 8, 32, 16, BgraBitmapInfo, pixels);
        var native = new FakeCoreGraphicsNative().AddDisplay(1, Rect(0, 0, 2, 1), image);
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var frame = backend.Capture(new ScreenRect(0, 0, 2, 1), CancellationToken.None);

        using var screenFrame = new ScreenFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels);
        Assert.Equal(2, screenFrame.Width);
        Assert.Equal(1, screenFrame.Height);
        Assert.Equal(new ScreenPixelColor(11, 0, 0), screenFrame.GetPixel(new ScreenPoint(0, 0)));
        Assert.Equal(new ScreenPixelColor(33, 0, 0), screenFrame.GetPixel(new ScreenPoint(1, 0)));
    }

    [Fact]
    public void Capture_PassesGlobalDisplayCoordinatesToCoreGraphics()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(-3, -2, 3, 2), Image(1, 1, 4, [Pixel(9, 8, 7)]));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var frame = backend.Capture(new ScreenRect(-2, -1, 1, 1), CancellationToken.None);

        using var screenFrame = new ScreenFrame(frame.LogicalBounds, frame.Stride, frame.PixelFormat, frame.Pixels);
        Assert.Equal(new ScreenRect(-2, -1, 1, 1), screenFrame.LogicalBounds);
        Assert.Equal(new ScreenPixelColor(9, 8, 7), screenFrame.GetPixel(new ScreenPoint(-2, -1)));
        Assert.Equal(new ScreenRect(-2, -1, 1, 1), native.LastCaptureRect);
    }

    [Fact]
    public void Capture_WhenNoDisplayIntersects_ThrowsCaptureFailure()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(0, 0, 1, 1), SolidImage(1, 1, Pixel(1, 1, 1)));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        Assert.Throws<InvalidOperationException>(() => backend.Capture(new ScreenRect(2, 2, 1, 1), CancellationToken.None));
    }

    [Fact]
    public void Capture_WhenSourceStrideIsTooSmall_ThrowsCaptureFailure()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(0, 0, 2, 1), new MacOSCapturedImage(2, 1, 8, 32, 4, BgraBitmapInfo, new byte[4]));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var exception = Assert.Throws<InvalidOperationException>(() => backend.Capture(new ScreenRect(0, 0, 2, 1), CancellationToken.None));

        Assert.Contains("stride", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Capture_WhenSourceDataIsTooShort_ThrowsCaptureFailure()
    {
        var native = new FakeCoreGraphicsNative()
            .AddDisplay(1, Rect(0, 0, 1, 2), new MacOSCapturedImage(1, 2, 8, 32, 4, BgraBitmapInfo, new byte[4]));
        var backend = new CoreGraphicsMacOSScreenCaptureBackend(native);

        var exception = Assert.Throws<InvalidOperationException>(() => backend.Capture(new ScreenRect(0, 0, 1, 2), CancellationToken.None));

        Assert.Contains("data length", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CoreGraphics.CGRect Rect(double x, double y, double width, double height) => new()
    {
        origin = new CoreGraphics.CGPoint { X = x, Y = y },
        size = new CoreGraphics.CGSize { width = width, height = height }
    };

    private static byte[] Pixel(byte red, byte green, byte blue) => [blue, green, red, 0xFF];

    private static MacOSCapturedImage SolidImage(int width, int height, byte[] pixel)
    {
        var sourcePixels = new List<byte>();
        for (var index = 0; index < width * height; index++)
        {
            sourcePixels.AddRange(pixel);
        }

        return new MacOSCapturedImage(width, height, 8, 32, width * 4, BgraBitmapInfo, sourcePixels.ToArray());
    }

    private static MacOSCapturedImage Image(int width, int height, int bytesPerRow, IReadOnlyList<byte[]> pixels)
    {
        var sourcePixels = new byte[bytesPerRow * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                WritePixel(sourcePixels, width, x, y, pixels[y * width + x], bytesPerRow);
            }
        }

        return new MacOSCapturedImage(width, height, 8, 32, bytesPerRow, BgraBitmapInfo, sourcePixels);
    }

    private static void WritePixel(byte[] pixels, int width, int x, int y, byte[] pixel, int? bytesPerRow = null)
    {
        var offset = y * (bytesPerRow ?? width * 4) + x * 4;
        Array.Copy(pixel, 0, pixels, offset, 4);
    }

    private sealed class FakeCoreGraphicsNative : IMacOSCoreGraphicsNative
    {
        private readonly Dictionary<uint, CoreGraphics.CGRect> _bounds = [];
        private readonly Dictionary<uint, MacOSCapturedImage> _images = [];

        public ScreenRect? LastCaptureRect { get; private set; }

        public FakeCoreGraphicsNative AddDisplay(uint display, CoreGraphics.CGRect bounds, MacOSCapturedImage image)
        {
            _bounds.Add(display, bounds);
            _images.Add(display, image);
            return this;
        }

        public uint GetActiveDisplayCount() => checked((uint)_bounds.Count);

        public uint[] GetActiveDisplays(uint count) => _bounds.Keys.Take(checked((int)count)).ToArray();

        public uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect)
        {
            var screenRect = ToScreenRect(rect);
            return _bounds
                .Where(pair => Intersects(screenRect, ToScreenRect(pair.Value)))
                .Select(pair => pair.Key)
                .ToArray();
        }

        public CoreGraphics.CGRect GetDisplayBounds(uint display) => _bounds[display];

        public MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect)
        {
            LastCaptureRect = ToScreenRect(rect);
            return _images[display];
        }

        private static ScreenRect ToScreenRect(CoreGraphics.CGRect rect) => new(
            (int)rect.origin.X,
            (int)rect.origin.Y,
            (int)rect.size.width,
            (int)rect.size.height);

        private static bool Intersects(ScreenRect left, ScreenRect right) =>
            left.X < right.Right && left.Right > right.X && left.Y < right.Bottom && left.Bottom > right.Y;
    }
}
