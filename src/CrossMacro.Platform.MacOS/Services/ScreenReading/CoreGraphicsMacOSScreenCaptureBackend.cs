
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class CoreGraphicsMacOSScreenCaptureBackend : IMacOSScreenCaptureBackend
{
    private readonly IMacOSCoreGraphicsNative _native;

    public CoreGraphicsMacOSScreenCaptureBackend()
        : this(new MacOSCoreGraphicsNative()) { /* Empty */ }

    internal CoreGraphicsMacOSScreenCaptureBackend(IMacOSCoreGraphicsNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public ScreenRect GetVirtualScreenBounds()
    {
        return GetVirtualScreenBounds(_native);
    }

    internal static ScreenRect GetVirtualScreenBounds(IMacOSCoreGraphicsNative native)
    {
        ArgumentNullException.ThrowIfNull(native);

        var displays = GetActiveDisplays(native);
        if (displays.Length is 0)
        {
            throw new BackendUnavailableException("CoreGraphics did not report any active displays.");
        }

        var bounds = ToScreenRect(native.GetDisplayBounds(displays[0]));
        for (var index = 1; index < displays.Length; index++)
        {
            bounds = Union(bounds, ToScreenRect(native.GetDisplayBounds(displays[index])));
        }

        return bounds;
    }

    public MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displays = _native.GetDisplaysWithRect(ToCGRect(region));
        if (displays.Length is 0)
        {
            throw new InvalidOperationException($"CoreGraphics found no displays intersecting region {region}.");
        }

        var stride = checked(region.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
        var pixels = new byte[checked(stride * region.Height)];
        var validPixelMask = new byte[checked(region.Width * region.Height)];

        foreach (var display in displays)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var displayBounds = ToScreenRect(_native.GetDisplayBounds(display));
            var intersection = Intersect(region, displayBounds);
            if (intersection is not { } sourceRect)
            {
                continue;
            }

            var displayLocalRect = new ScreenRect(
                checked(sourceRect.X - displayBounds.X),
                checked(sourceRect.Y - displayBounds.Y),
                sourceRect.Width,
                sourceRect.Height);
            using var image = _native.CreateImageForRect(display, ToCGRect(displayLocalRect));
            if (image.IsEmpty)
            {
                throw new InvalidOperationException($"CoreGraphics returned an empty image for display {display} and region {sourceRect}.");
            }

            CopyImageToFrame(image, sourceRect, region, stride, pixels, validPixelMask);
        }

        return new MacOSScreenCaptureFrame(region, stride, ScreenPixelFormat.Bgra8888, pixels, validPixelMask);
    }

    private static uint[] GetActiveDisplays(IMacOSCoreGraphicsNative native)
    {
        var count = native.GetActiveDisplayCount();
        if (count == 0)
        {
            return [];
        }

        return native.GetActiveDisplays(count);
    }

    private static void CopyImageToFrame(
        MacOSCapturedImage image,
        ScreenRect sourceRect,
        ScreenRect targetRect,
        int targetStride,
        byte[] targetPixels,
        byte[] targetValidPixelMask)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.BytesPerRow <= 0)
        {
            throw new InvalidOperationException($"CoreGraphics returned invalid image dimensions {image.Width.ToString(CultureInfo.InvariantCulture)}x{image.Height.ToString(CultureInfo.InvariantCulture)} with stride {image.BytesPerRow.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (image.BitsPerComponent is not 8 || image.BitsPerPixel is not 32)
        {
            throw new InvalidOperationException($"CoreGraphics returned unsupported pixel layout: {image.BitsPerComponent.ToString(CultureInfo.InvariantCulture)} bits/component, {image.BitsPerPixel.ToString(CultureInfo.InvariantCulture)} bits/pixel.");
        }

        var minimumRowBytes = checked(image.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
        if (image.BytesPerRow < minimumRowBytes)
        {
            throw new InvalidOperationException($"CoreGraphics image stride {image.BytesPerRow.ToString(CultureInfo.InvariantCulture)} is smaller than its pixel width {image.Width.ToString(CultureInfo.InvariantCulture)}.");
        }

        var minimumDataLength = checked(image.BytesPerRow * image.Height);
        if (image.Pixels.Length < minimumDataLength)
        {
            throw new InvalidOperationException($"CoreGraphics image data length {image.Pixels.Length.ToString(CultureInfo.InvariantCulture)} is smaller than its declared size {minimumDataLength.ToString(CultureInfo.InvariantCulture)}.");
        }

        var sourceFormat = ResolveSourceFormat(image.BitmapInfo);
        var scaleX = image.Width / (double)sourceRect.Width;
        var scaleY = image.Height / (double)sourceRect.Height;

        for (var logicalY = 0; logicalY < sourceRect.Height; logicalY++)
        {
            var sourceY = Clamp((int)((logicalY + 0.5d) * scaleY), 0, image.Height - 1);
            var targetY = sourceRect.Y - targetRect.Y + logicalY;
            for (var logicalX = 0; logicalX < sourceRect.Width; logicalX++)
            {
                var sourceX = Clamp((int)((logicalX + 0.5d) * scaleX), 0, image.Width - 1);
                var sourceOffset = checked((sourceY * image.BytesPerRow) + (sourceX * 4));
                var targetOffset = checked((targetY * targetStride) + ((sourceRect.X - targetRect.X + logicalX) * 4));
                WriteBgraPixel(image.Pixels, sourceOffset, sourceFormat, targetPixels, targetOffset);
                var targetMaskOffset = checked((targetY * targetRect.Width) + sourceRect.X - targetRect.X + logicalX);
                targetValidPixelMask[targetMaskOffset] = 1;
            }
        }
    }

    private static MacOSSourcePixelFormat ResolveSourceFormat(CoreGraphics.CGBitmapInfo bitmapInfo)
    {
        var info = (uint)bitmapInfo;
        var byteOrder = info & CoreGraphics.kCGBitmapByteOrderMask;
        var alphaInfo = info & CoreGraphics.kCGBitmapAlphaInfoMask;

        return (byteOrder, alphaInfo) switch
        {
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaPremultipliedFirst) => MacOSSourcePixelFormat.BgraPremultiplied,
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaFirst) => MacOSSourcePixelFormat.Bgra,
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaNoneSkipFirst) => MacOSSourcePixelFormat.BgraOpaque,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaPremultipliedLast) => MacOSSourcePixelFormat.RgbaPremultiplied,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaLast) => MacOSSourcePixelFormat.Rgba,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaNoneSkipLast) => MacOSSourcePixelFormat.RgbaOpaque,
            _ => throw new InvalidOperationException($"CoreGraphics returned unsupported bitmap info 0x{info:X}."),
        };
    }

    private static void WriteBgraPixel(byte[] sourcePixels, int sourceOffset, MacOSSourcePixelFormat sourceFormat, byte[] targetPixels, int targetOffset)
    {
        var isBgra = sourceFormat is MacOSSourcePixelFormat.Bgra or MacOSSourcePixelFormat.BgraPremultiplied or MacOSSourcePixelFormat.BgraOpaque;
        var blue = sourcePixels[sourceOffset + (isBgra ? 0 : 2)];
        var green = sourcePixels[sourceOffset + 1];
        var red = sourcePixels[sourceOffset + (isBgra ? 2 : 0)];
        var alpha = sourceFormat is MacOSSourcePixelFormat.BgraOpaque or MacOSSourcePixelFormat.RgbaOpaque
            ? byte.MaxValue
            : sourcePixels[sourceOffset + 3];

        if (sourceFormat is MacOSSourcePixelFormat.BgraPremultiplied or MacOSSourcePixelFormat.RgbaPremultiplied)
        {
            blue = Unpremultiply(blue, alpha);
            green = Unpremultiply(green, alpha);
            red = Unpremultiply(red, alpha);
        }

        targetPixels[targetOffset] = blue;
        targetPixels[targetOffset + 1] = green;
        targetPixels[targetOffset + 2] = red;
        targetPixels[targetOffset + 3] = byte.MaxValue;
    }

    private static byte Unpremultiply(byte value, byte alpha) => alpha is 0 ? (byte)0 : (byte)Math.Min(byte.MaxValue, ((value * 255) + (alpha / 2)) / alpha);

    private static ScreenRect? Intersect(ScreenRect left, ScreenRect right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var rightEdge = Math.Min(left.Right, right.Right);
        var bottom = Math.Min(left.Bottom, right.Bottom);
        return rightEdge > x && bottom > y ? new ScreenRect(x, y, rightEdge - x, bottom - y) : null;
    }

    private static ScreenRect Union(ScreenRect left, ScreenRect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.Right, right.Right);
        var bottom = Math.Max(left.Bottom, right.Bottom);
        return new ScreenRect(x, y, rightEdge - x, bottom - y);
    }

    private static ScreenRect ToScreenRect(CoreGraphics.CGRect rect) => new(
        checked((int)Math.Floor(rect.origin.X)),
        checked((int)Math.Floor(rect.origin.Y)),
        checked((int)Math.Ceiling(rect.size.width)),
        checked((int)Math.Ceiling(rect.size.height)));

    private static CoreGraphics.CGRect ToCGRect(ScreenRect rect) => new()
    {
        origin = new CoreGraphics.CGPoint { X = rect.X, Y = rect.Y },
        size = new CoreGraphics.CGSize { width = rect.Width, height = rect.Height },
    };

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private enum MacOSSourcePixelFormat
    {
        Bgra,
        BgraPremultiplied,
        BgraOpaque,
        Rgba,
        RgbaPremultiplied,
        RgbaOpaque,
    }
}
