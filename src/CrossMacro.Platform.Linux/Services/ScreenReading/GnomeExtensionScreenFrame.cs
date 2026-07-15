using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class GnomeExtensionScreenFrame : IDisposable
{
    public GnomeExtensionScreenFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, byte[] pixels)
    {
        LogicalBounds = logicalBounds;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels;
    }

    public ScreenRect LogicalBounds { get; }
    public int Stride { get; }
    public ScreenPixelFormat PixelFormat { get; }
    public byte[] Pixels { get; }

    public void Dispose()
    {
    }
}
