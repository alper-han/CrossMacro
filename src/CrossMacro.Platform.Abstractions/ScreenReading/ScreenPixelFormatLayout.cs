namespace CrossMacro.Platform.Abstractions.ScreenReading;

/// <summary>Maps a transport pixel format to its normalized byte layout.</summary>
public static class ScreenPixelFormatLayout
{
    /// <summary>Gets the byte layout used to read one pixel in <paramref name="pixelFormat"/>.</summary>
    public static ScreenPixelLayout Get(ScreenPixelFormat pixelFormat) => pixelFormat switch
    {
        ScreenPixelFormat.Rgb24 => new ScreenPixelLayout(BytesPerPixel: 3, RedOffset: 0, GreenOffset: 1, BlueOffset: 2, AlphaOffset: -1),
        ScreenPixelFormat.Bgr24 => new ScreenPixelLayout(BytesPerPixel: 3, RedOffset: 2, GreenOffset: 1, BlueOffset: 0, AlphaOffset: -1),
        ScreenPixelFormat.Xrgb8888 => new ScreenPixelLayout(BytesPerPixel: 4, RedOffset: 2, GreenOffset: 1, BlueOffset: 0, AlphaOffset: -1),
        ScreenPixelFormat.Bgra8888 => new ScreenPixelLayout(BytesPerPixel: 4, RedOffset: 2, GreenOffset: 1, BlueOffset: 0, AlphaOffset: 3),
        ScreenPixelFormat.Abgr8888 => new ScreenPixelLayout(BytesPerPixel: 4, RedOffset: 0, GreenOffset: 1, BlueOffset: 2, AlphaOffset: 3),
        ScreenPixelFormat.Xbgr8888 => new ScreenPixelLayout(BytesPerPixel: 4, RedOffset: 0, GreenOffset: 1, BlueOffset: 2, AlphaOffset: -1),
        _ => throw new ArgumentOutOfRangeException(nameof(pixelFormat), pixelFormat, "Unsupported screen pixel format."),
    };
}
