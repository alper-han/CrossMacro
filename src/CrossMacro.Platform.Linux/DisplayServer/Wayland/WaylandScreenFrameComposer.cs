using System.Buffers;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandScreenFrameComposer : IDisposable
{
    internal const int MaxCanvasPixels = 16_777_216;
    internal const long MaxCanvasBytes = 128L * 1024 * 1024;
    private const byte ValidPixel = 1;
    private readonly int _pixelByteCount;
    private readonly int _validPixelCount;
    private byte[]? _pixels;
    private byte[]? _validPixelMask;

    private WaylandScreenFrameComposer(
        ScreenRect logicalBounds,
        int stride,
        int pixelByteCount,
        int validPixelCount,
        byte[] pixels,
        byte[] validPixelMask)
    {
        LogicalBounds = logicalBounds;
        Stride = stride;
        _pixelByteCount = pixelByteCount;
        _validPixelCount = validPixelCount;
        _pixels = pixels;
        _validPixelMask = validPixelMask;
    }

    public const ScreenPixelFormat TargetPixelFormat = ScreenPixelFormat.Bgra8888;

    public ScreenRect LogicalBounds { get; }

    public int Stride { get; }

    public static WaylandScreenFrameComposer Create(ScreenRect logicalBounds)
    {
        var stride = checked(logicalBounds.Width * ScreenFrame.GetBytesPerPixel(TargetPixelFormat));
        var pixelCount = checked((long)logicalBounds.Width * logicalBounds.Height);
        var pixelByteCountLong = checked(pixelCount * ScreenFrame.GetBytesPerPixel(TargetPixelFormat));
        var canvasByteCount = checked(pixelByteCountLong + pixelCount);
        if (pixelCount > MaxCanvasPixels || canvasByteCount > MaxCanvasBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalBounds),
                logicalBounds,
                $"The stitched Wayland canvas exceeds the internal limit of {MaxCanvasPixels} pixels or {MaxCanvasBytes} bytes.");
        }

        var pixelByteCount = checked((int)pixelByteCountLong);
        var validPixelCount = checked((int)pixelCount);
        byte[]? pixels = null;
        byte[]? validPixelMask = null;

        try
        {
            pixels = ArrayPool<byte>.Shared.Rent(pixelByteCount);
            validPixelMask = ArrayPool<byte>.Shared.Rent(validPixelCount);
            Array.Clear(pixels, 0, pixelByteCount);
            Array.Clear(validPixelMask, 0, validPixelCount);
            return new WaylandScreenFrameComposer(logicalBounds, stride, pixelByteCount, validPixelCount, pixels, validPixelMask);
        }
        catch
        {
            if (pixels is not null)
            {
                ArrayPool<byte>.Shared.Return(pixels);
            }

            if (validPixelMask is not null)
            {
                ArrayPool<byte>.Shared.Return(validPixelMask);
            }

            throw;
        }
    }

    public static ScreenRect Union(ScreenRect first, ScreenRect second)
    {
        var x = Math.Min(first.X, second.X);
        var y = Math.Min(first.Y, second.Y);
        var right = Math.Max((long)first.X + first.Width, (long)second.X + second.Width);
        var bottom = Math.Max((long)first.Y + first.Height, (long)second.Y + second.Height);
        return new ScreenRect(
            x,
            y,
            checked((int)(right - x)),
            checked((int)(bottom - y)));
    }

    public static ScreenRect? Intersect(ScreenRect first, ScreenRect second)
    {
        var x = Math.Max(first.X, second.X);
        var y = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);

        return right > x && bottom > y ? new ScreenRect(x, y, right - x, bottom - y) : null;
    }

    public void CopySource(
        ReadOnlySpan<byte> sourcePixels,
        int sourceStride,
        ScreenPixelFormat sourceFormat,
        int sourcePhysicalWidth,
        int sourcePhysicalHeight,
        ScreenRect sourceLogicalBounds,
        ScreenRect intersection)
    {
        var targetPixels = _pixels ?? throw new ObjectDisposedException(nameof(WaylandScreenFrameComposer));
        var targetValidPixelMask = _validPixelMask ?? throw new ObjectDisposedException(nameof(WaylandScreenFrameComposer));

        if (!LogicalBounds.Contains(intersection))
        {
            throw new ArgumentOutOfRangeException(nameof(intersection), intersection, "The source intersection is outside the target frame bounds.");
        }

        if (!sourceLogicalBounds.Contains(intersection))
        {
            throw new ArgumentOutOfRangeException(nameof(intersection), intersection, "The source intersection is outside the source frame bounds.");
        }

        var sourceBytesPerPixel = ScreenFrame.GetBytesPerPixel(sourceFormat);
        if (sourcePhysicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePhysicalWidth), sourcePhysicalWidth, "Source physical width must be positive.");
        }

        if (sourcePhysicalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePhysicalHeight), sourcePhysicalHeight, "Source physical height must be positive.");
        }

        var minimumSourceStride = checked(sourcePhysicalWidth * sourceBytesPerPixel);
        if (sourceStride < minimumSourceStride)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceStride), sourceStride, "Source stride is smaller than the source physical row width.");
        }

        var minimumSourceLength = checked(sourceStride * sourcePhysicalHeight);
        if (sourcePixels.Length < minimumSourceLength)
        {
            throw new ArgumentException("Source pixel memory is smaller than the declared physical frame dimensions.", nameof(sourcePixels));
        }

        var scaleX = sourcePhysicalWidth / (double)sourceLogicalBounds.Width;
        var scaleY = sourcePhysicalHeight / (double)sourceLogicalBounds.Height;

        for (var logicalY = 0; logicalY < intersection.Height; logicalY++)
        {
            var sourceLogicalY = intersection.Y - sourceLogicalBounds.Y + logicalY;
            var sourceY = Clamp((int)((sourceLogicalY + 0.5d) * scaleY), 0, sourcePhysicalHeight - 1);
            var targetY = intersection.Y - LogicalBounds.Y + logicalY;

            for (var logicalX = 0; logicalX < intersection.Width; logicalX++)
            {
                var sourceLogicalX = intersection.X - sourceLogicalBounds.X + logicalX;
                var sourceX = Clamp((int)((sourceLogicalX + 0.5d) * scaleX), 0, sourcePhysicalWidth - 1);
                var targetX = intersection.X - LogicalBounds.X + logicalX;

                var sourceOffset = checked((sourceY * sourceStride) + (sourceX * sourceBytesPerPixel));
                var targetOffset = checked((targetY * Stride) + (targetX * ScreenFrame.GetBytesPerPixel(TargetPixelFormat)));

                WriteBgraPixel(sourcePixels, sourceOffset, sourceFormat, targetPixels, targetOffset);
                targetValidPixelMask[checked((targetY * LogicalBounds.Width) + targetX)] = ValidPixel;
            }
        }
    }

    public WaylandComposedFrame Complete()
    {
        var pixels = _pixels ?? throw new ObjectDisposedException(nameof(WaylandScreenFrameComposer));
        var validPixelMask = _validPixelMask ?? throw new ObjectDisposedException(nameof(WaylandScreenFrameComposer));
        _pixels = null;
        _validPixelMask = null;

        var hasInvalidPixels = validPixelMask.AsSpan(0, _validPixelCount).Contains((byte)0);
        if (!hasInvalidPixels)
        {
            ArrayPool<byte>.Shared.Return(validPixelMask);
            validPixelMask = null;
        }

        ScreenFrameValidityIndex? validityIndex = null;
        try
        {
            if (validPixelMask is not null)
            {
                validityIndex = ScreenFrameValidityIndex.Create(validPixelMask.AsSpan(0, _validPixelCount), LogicalBounds.Width, LogicalBounds.Height);
            }
        }
        catch
        {
            if (validPixelMask is not null)
            {
                ArrayPool<byte>.Shared.Return(validPixelMask);
            }

            ArrayPool<byte>.Shared.Return(pixels);
            throw;
        }

        return new WaylandComposedFrame(
            LogicalBounds,
            Stride,
            TargetPixelFormat,
            new ReadOnlyMemory<byte>(pixels, 0, _pixelByteCount),
            validPixelMask is null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(validPixelMask, 0, _validPixelCount),
            pixels,
            validPixelMask,
            validityIndex);
    }

    public void Dispose()
    {
        if (_pixels is not null)
        {
            ArrayPool<byte>.Shared.Return(_pixels);
            _pixels = null;
        }

        if (_validPixelMask is not null)
        {
            ArrayPool<byte>.Shared.Return(_validPixelMask);
            _validPixelMask = null;
        }
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static void WriteBgraPixel(ReadOnlySpan<byte> source, int sourceOffset, ScreenPixelFormat sourceFormat, byte[] target, int targetOffset)
    {
        switch (sourceFormat)
        {
            case ScreenPixelFormat.Rgb24:
                target[targetOffset] = source[sourceOffset + 2];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset];
                target[targetOffset + 3] = 255;
                break;
            case ScreenPixelFormat.Bgr24:
                target[targetOffset] = source[sourceOffset];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset + 2];
                target[targetOffset + 3] = 255;
                break;
            case ScreenPixelFormat.Xrgb8888:
                target[targetOffset] = source[sourceOffset];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset + 2];
                target[targetOffset + 3] = 255;
                break;
            case ScreenPixelFormat.Bgra8888:
                target[targetOffset] = source[sourceOffset];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset + 2];
                target[targetOffset + 3] = source[sourceOffset + 3];
                break;
            case ScreenPixelFormat.Abgr8888:
                target[targetOffset] = source[sourceOffset + 2];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset];
                target[targetOffset + 3] = source[sourceOffset + 3];
                break;
            case ScreenPixelFormat.Xbgr8888:
                target[targetOffset] = source[sourceOffset + 2];
                target[targetOffset + 1] = source[sourceOffset + 1];
                target[targetOffset + 2] = source[sourceOffset];
                target[targetOffset + 3] = 255;
                break;
            default:
                throw new InvalidOperationException($"Unsupported screen pixel format '{sourceFormat}'.");
        }
    }
}

internal sealed class WaylandComposedFrame : IDisposable
{
    private byte[]? _pixels;
    private byte[]? _validPixelMask;
    private ScreenFrameValidityIndex? _validityIndex;

    public WaylandComposedFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        ReadOnlyMemory<byte> validPixelMask,
        byte[] pixelArray,
        byte[]? validPixelMaskArray,
        ScreenFrameValidityIndex? validityIndex)
    {
        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        ValidPixelMask = validPixelMask;
        _pixels = pixelArray;
        _validPixelMask = validPixelMaskArray;
        _validityIndex = validityIndex;
    }

    public ScreenRect LogicalBounds { get; }

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ReadOnlyMemory<byte> ValidPixelMask { get; }

    public bool IsFullyValid => ValidPixelMask.IsEmpty && _validityIndex is null;

    public ScreenFrameValidityIndex? ValidityIndex => _validityIndex;

    public void Dispose()
    {
        if (_pixels is not null)
        {
            ArrayPool<byte>.Shared.Return(_pixels);
            _pixels = null;
        }

        if (_validPixelMask is not null)
        {
            ArrayPool<byte>.Shared.Return(_validPixelMask);
            _validPixelMask = null;
        }

        _validityIndex?.Dispose();
        _validityIndex = null;
    }
}
