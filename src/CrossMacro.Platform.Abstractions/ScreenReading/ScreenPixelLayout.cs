using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Abstractions.ScreenReading;

/// <summary>Normalized byte layout for one supported screen pixel format.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScreenPixelLayout(
    int BytesPerPixel,
    int RedOffset,
    int GreenOffset,
    int BlueOffset,
    int AlphaOffset)
{
    /// <summary>Gets whether the source format carries a meaningful alpha byte.</summary>
    public bool HasAlphaChannel => AlphaOffset >= 0;
}
