using System.Runtime.InteropServices;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class CoreGraphicsMacOSScreenCaptureBackend : IMacOSScreenCaptureBackend
{
    private readonly IMacOSCoreGraphicsNative _native;

    public CoreGraphicsMacOSScreenCaptureBackend()
        : this(new MacOSCoreGraphicsNative())
    {
    }

    internal CoreGraphicsMacOSScreenCaptureBackend(IMacOSCoreGraphicsNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public ScreenRect GetVirtualScreenBounds()
    {
        var displays = GetActiveDisplays();
        if (displays.Length == 0)
        {
            throw new BackendUnavailableException("CoreGraphics did not report any active displays.");
        }

        var bounds = ToScreenRect(_native.GetDisplayBounds(displays[0]));
        for (var index = 1; index < displays.Length; index++)
        {
            bounds = Union(bounds, ToScreenRect(_native.GetDisplayBounds(displays[index])));
        }

        return bounds;
    }

    public MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displays = _native.GetDisplaysWithRect(ToCGRect(region));
        if (displays.Length == 0)
        {
            throw new InvalidOperationException($"CoreGraphics found no displays intersecting region {region}.");
        }

        var stride = checked(region.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
        var pixels = new byte[checked(stride * region.Height)];

        foreach (var display in displays)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var displayBounds = ToScreenRect(_native.GetDisplayBounds(display));
            var intersection = Intersect(region, displayBounds);
            if (intersection is not { } sourceRect)
            {
                continue;
            }

            using var image = _native.CreateImageForRect(display, ToCGRect(sourceRect));
            if (image.IsEmpty)
            {
                throw new InvalidOperationException($"CoreGraphics returned an empty image for display {display} and region {sourceRect}.");
            }

            CopyImageToFrame(image, sourceRect, region, stride, pixels);
        }

        return new MacOSScreenCaptureFrame(region, stride, ScreenPixelFormat.Bgra8888, pixels);
    }

    private uint[] GetActiveDisplays()
    {
        var count = _native.GetActiveDisplayCount();
        if (count == 0)
        {
            return [];
        }

        return _native.GetActiveDisplays(count);
    }

    private static void CopyImageToFrame(MacOSCapturedImage image, ScreenRect sourceRect, ScreenRect targetRect, int targetStride, byte[] targetPixels)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.BytesPerRow <= 0)
        {
            throw new InvalidOperationException($"CoreGraphics returned invalid image dimensions {image.Width}x{image.Height} with stride {image.BytesPerRow}.");
        }

        if (image.BitsPerComponent != 8 || image.BitsPerPixel != 32)
        {
            throw new InvalidOperationException($"CoreGraphics returned unsupported pixel layout: {image.BitsPerComponent} bits/component, {image.BitsPerPixel} bits/pixel.");
        }

        var minimumRowBytes = checked(image.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
        if (image.BytesPerRow < minimumRowBytes)
        {
            throw new InvalidOperationException($"CoreGraphics image stride {image.BytesPerRow} is smaller than its pixel width {image.Width}.");
        }

        var minimumDataLength = checked(image.BytesPerRow * image.Height);
        if (image.Pixels.Length < minimumDataLength)
        {
            throw new InvalidOperationException($"CoreGraphics image data length {image.Pixels.Length} is smaller than its declared size {minimumDataLength}.");
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
                var sourceOffset = checked(sourceY * image.BytesPerRow + sourceX * 4);
                var targetOffset = checked(targetY * targetStride + (sourceRect.X - targetRect.X + logicalX) * 4);
                WriteBgraPixel(image.Pixels, sourceOffset, sourceFormat, targetPixels, targetOffset);
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
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaPremultipliedFirst) => MacOSSourcePixelFormat.Bgra,
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaFirst) => MacOSSourcePixelFormat.Bgra,
            (CoreGraphics.kCGBitmapByteOrder32Little, (uint)CoreGraphics.CGBitmapInfo.AlphaNoneSkipFirst) => MacOSSourcePixelFormat.Bgra,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaPremultipliedLast) => MacOSSourcePixelFormat.Rgba,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaLast) => MacOSSourcePixelFormat.Rgba,
            (CoreGraphics.kCGBitmapByteOrder32Big, (uint)CoreGraphics.CGBitmapInfo.AlphaNoneSkipLast) => MacOSSourcePixelFormat.Rgba,
            _ => throw new InvalidOperationException($"CoreGraphics returned unsupported bitmap info 0x{info:X}.")
        };
    }

    private static void WriteBgraPixel(byte[] sourcePixels, int sourceOffset, MacOSSourcePixelFormat sourceFormat, byte[] targetPixels, int targetOffset)
    {
        if (sourceFormat == MacOSSourcePixelFormat.Bgra)
        {
            targetPixels[targetOffset] = sourcePixels[sourceOffset];
            targetPixels[targetOffset + 1] = sourcePixels[sourceOffset + 1];
            targetPixels[targetOffset + 2] = sourcePixels[sourceOffset + 2];
            targetPixels[targetOffset + 3] = sourcePixels[sourceOffset + 3];
            return;
        }

        targetPixels[targetOffset] = sourcePixels[sourceOffset + 2];
        targetPixels[targetOffset + 1] = sourcePixels[sourceOffset + 1];
        targetPixels[targetOffset + 2] = sourcePixels[sourceOffset];
        targetPixels[targetOffset + 3] = sourcePixels[sourceOffset + 3];
    }

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
        size = new CoreGraphics.CGSize { width = rect.Width, height = rect.Height }
    };

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private enum MacOSSourcePixelFormat
    {
        Bgra,
        Rgba
    }
}

internal interface IMacOSCoreGraphicsNative
{
    uint GetActiveDisplayCount();

    uint[] GetActiveDisplays(uint count);

    uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect);

    CoreGraphics.CGRect GetDisplayBounds(uint display);

    MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect);
}

internal sealed record MacOSCapturedImage(
    int Width,
    int Height,
    int BitsPerComponent,
    int BitsPerPixel,
    int BytesPerRow,
    CoreGraphics.CGBitmapInfo BitmapInfo,
    byte[] Pixels) : IDisposable
{
    public bool IsEmpty => Width == 0 || Height == 0 || Pixels.Length == 0;

    public void Dispose()
    {
    }
}

internal sealed class MacOSCoreGraphicsNative : IMacOSCoreGraphicsNative
{
    public uint GetActiveDisplayCount()
    {
        var error = CoreGraphics.CGGetActiveDisplayList(0, null, out var count);
        ThrowIfFailed(error, "CGGetActiveDisplayList count");
        return count;
    }

    public uint[] GetActiveDisplays(uint count)
    {
        var displays = new uint[count];
        var error = CoreGraphics.CGGetActiveDisplayList(count, displays, out var actualCount);
        ThrowIfFailed(error, "CGGetActiveDisplayList");
        return actualCount == count ? displays : displays[..checked((int)actualCount)];
    }

    public uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect)
    {
        var countError = CoreGraphics.CGGetDisplaysWithRect(rect, 0, null, out var count);
        ThrowIfFailed(countError, "CGGetDisplaysWithRect count");
        if (count == 0)
        {
            return [];
        }

        var displays = new uint[count];
        var error = CoreGraphics.CGGetDisplaysWithRect(rect, count, displays, out var actualCount);
        ThrowIfFailed(error, "CGGetDisplaysWithRect");
        return actualCount == count ? displays : displays[..checked((int)actualCount)];
    }

    public CoreGraphics.CGRect GetDisplayBounds(uint display) => CoreGraphics.CGDisplayBounds(display);

    public MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect)
    {
        var image = CoreGraphics.CGDisplayCreateImageForRect(display, rect);
        if (image == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CGDisplayCreateImageForRect failed for display {display}.");
        }

        try
        {
            return CopyImage(image);
        }
        finally
        {
            CoreGraphics.CGImageRelease(image);
        }
    }

    private static MacOSCapturedImage CopyImage(IntPtr image)
    {
        var provider = CoreGraphics.CGImageGetDataProvider(image);
        if (provider == IntPtr.Zero)
        {
            throw new InvalidOperationException("CGImageGetDataProvider returned null.");
        }

        var data = CoreGraphics.CGDataProviderCopyData(provider);
        if (data == IntPtr.Zero)
        {
            throw new InvalidOperationException("CGDataProviderCopyData returned null.");
        }

        try
        {
            var length = checked((int)CoreFoundation.CFDataGetLength(data));
            var bytes = new byte[length];
            if (length > 0)
            {
                var source = CoreFoundation.CFDataGetBytePtr(data);
                if (source == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CFDataGetBytePtr returned null for non-empty image data.");
                }

                Marshal.Copy(source, bytes, 0, length);
            }

            return new MacOSCapturedImage(
                checked((int)CoreGraphics.CGImageGetWidth(image)),
                checked((int)CoreGraphics.CGImageGetHeight(image)),
                checked((int)CoreGraphics.CGImageGetBitsPerComponent(image)),
                checked((int)CoreGraphics.CGImageGetBitsPerPixel(image)),
                checked((int)CoreGraphics.CGImageGetBytesPerRow(image)),
                CoreGraphics.CGImageGetBitmapInfo(image),
                bytes);
        }
        finally
        {
            CoreFoundation.CFRelease(data);
        }
    }

    private static void ThrowIfFailed(CoreGraphics.CGError error, string operation)
    {
        if (error != CoreGraphics.CGError.Success)
        {
            throw new BackendUnavailableException($"{operation} failed with CoreGraphics error {(int)error}.");
        }
    }
}
