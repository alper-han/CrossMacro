using System.Buffers;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

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
