namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Pixels captured by one Wayland screen backend together with the ownership of their memory.
/// </summary>
public sealed class WaylandScreenFrame : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    /// <param name="logicalBounds">Desktop rectangle the frame covers.</param>
    /// <param name="physicalWidth">Buffer width in pixels; defaults to the logical width.</param>
    /// <param name="physicalHeight">Buffer height in pixels; defaults to the logical height.</param>
    /// <param name="bufferTransform">How the buffer is oriented relative to the logical image.</param>
    public WaylandScreenFrame(
        ScreenRect logicalBounds,
        int stride,
        ScreenPixelFormat pixelFormat,
        ReadOnlyMemory<byte> pixels,
        IDisposable? owner = null,
        ReadOnlyMemory<byte> validPixelMask = default,
        int? physicalWidth = null,
        int? physicalHeight = null,
        ScreenFrameValidityIndex? validityIndex = null,
        WaylandBufferTransform bufferTransform = WaylandBufferTransform.Normal)
    {
        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        var declaredPhysicalWidth = physicalWidth ?? logicalBounds.Width;
        var declaredPhysicalHeight = physicalHeight ?? logicalBounds.Height;
        if (declaredPhysicalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalWidth), declaredPhysicalWidth, "Wayland screen frame physical width must be positive.");
        }

        if (declaredPhysicalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalHeight), declaredPhysicalHeight, "Wayland screen frame physical height must be positive.");
        }

        var minimumStride = checked(declaredPhysicalWidth * bytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Wayland screen frame stride is smaller than its physical row width.");
        }

        var minimumLength = checked(stride * declaredPhysicalHeight);
        if (pixels.Length < minimumLength)
        {
            throw new ArgumentException("Wayland screen frame pixel memory is smaller than the declared physical frame dimensions.", nameof(pixels));
        }

        var validPixelCount = checked(logicalBounds.Width * logicalBounds.Height);
        if (!validPixelMask.IsEmpty && validPixelMask.Length < validPixelCount)
        {
            throw new ArgumentException("Wayland screen frame valid-pixel mask is smaller than the declared frame dimensions.", nameof(validPixelMask));
        }

        LogicalBounds = logicalBounds;
        PhysicalWidth = declaredPhysicalWidth;
        PhysicalHeight = declaredPhysicalHeight;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
        ValidPixelMask = validPixelMask.IsEmpty ? ReadOnlyMemory<byte>.Empty : validPixelMask.Slice(0, validPixelCount);
        ValidityIndex = validityIndex;
        BufferTransform = bufferTransform;
        _owner = owner;
    }

    public ScreenRect LogicalBounds { get; }

    public int PhysicalWidth { get; }

    public int PhysicalHeight { get; }

    public int Stride { get; }

    public ScreenPixelFormat PixelFormat { get; }

    public ReadOnlyMemory<byte> Pixels { get; }

    public ReadOnlyMemory<byte> ValidPixelMask { get; }

    public ScreenFrameValidityIndex? ValidityIndex { get; }

    public WaylandBufferTransform BufferTransform { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ValidityIndex?.Dispose();
        _owner?.Dispose();
    }
}
