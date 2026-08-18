
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandComposedFrame(
    ScreenRect logicalBounds,
    int stride,
    ScreenPixelFormat pixelFormat,
    ReadOnlyMemory<byte> pixels,
    ReadOnlyMemory<byte> validPixelMask,
    byte[] pixelArray,
    byte[]? validPixelMaskArray,
    ScreenFrameValidityIndex? validityIndex) : IDisposable
{
    private byte[]? _pixels = pixelArray;
    private byte[]? _validPixelMask = validPixelMaskArray;

    public ScreenRect LogicalBounds { get; } = logicalBounds;

    public int Stride { get; } = stride;

    public ScreenPixelFormat PixelFormat { get; } = pixelFormat;

    public ReadOnlyMemory<byte> Pixels { get; } = pixels;

    public ReadOnlyMemory<byte> ValidPixelMask { get; } = validPixelMask;
    public bool IsFullyValid => ValidPixelMask.IsEmpty && ValidityIndex is null;
    public ScreenFrameValidityIndex? ValidityIndex { get; private set; } = validityIndex;

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

        ValidityIndex?.Dispose();
        ValidityIndex = null;
    }
}
