
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Maps the formats advertised by the Wayland <c>wl_shm</c> interface to the
/// pixel formats consumed by CrossMacro's screen-frame pipeline.
/// </summary>
/// <remarks>
/// Both ext-image-copy-capture and wlr-screencopy report their shared-memory
/// buffer format using the same <c>wl_shm.format</c> values. Keeping the
/// mapping here prevents either capture path from making protocol-specific
/// assumptions about bytes per pixel or channel order.
/// </remarks>
internal static class WaylandShmFormats
{
    public const uint Argb8888 = 0x00000000;
    public const uint Xrgb8888 = 0x00000001;
    public const uint Rgb888 = 0x34324752;
    public const uint Bgr888 = 0x34324742;
    public const uint Abgr8888 = 0x34324241;
    public const uint Xbgr8888 = 0x34324258;

    public static bool TryMap(uint shmFormat, out ScreenPixelFormat pixelFormat)
    {
        switch (shmFormat)
        {
            case Argb8888:
                pixelFormat = ScreenPixelFormat.Bgra8888;
                return true;
            case Xrgb8888:
                pixelFormat = ScreenPixelFormat.Xrgb8888;
                return true;
            case Rgb888:
                // wl_shm describes the packed 24-bit value; little-endian
                // memory stores its RGB888 bytes as B, G, R.
                pixelFormat = ScreenPixelFormat.Bgr24;
                return true;
            case Bgr888:
                // Conversely, BGR888 is stored as R, G, B on little-endian
                // Linux systems supported by this backend.
                pixelFormat = ScreenPixelFormat.Rgb24;
                return true;
            case Abgr8888:
                pixelFormat = ScreenPixelFormat.Abgr8888;
                return true;
            case Xbgr8888:
                pixelFormat = ScreenPixelFormat.Xbgr8888;
                return true;
            default:
                pixelFormat = default;
                return false;
        }
    }

    public static bool TryGetStride(uint shmFormat, uint width, out int stride)
    {
        if (!TryMap(shmFormat, out var pixelFormat))
        {
            stride = 0;
            return false;
        }

        var bytesPerPixel = ScreenFrame.GetBytesPerPixel(pixelFormat);
        if (width > (uint)(int.MaxValue / bytesPerPixel))
        {
            stride = 0;
            return false;
        }

        stride = checked((int)width * bytesPerPixel);
        return true;
    }

    public static bool TrySelectPreferredPixelFormat(ReadOnlySpan<uint> advertisedFormats, out ScreenPixelFormat pixelFormat)
    {
        if (TrySelectPreferredShmFormat(advertisedFormats, out var shmFormat))
        {
            return TryMap(shmFormat, out pixelFormat);
        }

        pixelFormat = default;
        return false;
    }

    public static bool TrySelectPreferredShmFormat(ReadOnlySpan<uint> advertisedFormats, out uint shmFormat)
    {
        var hasSelected = false;
        shmFormat = default;

        foreach (var advertisedFormat in advertisedFormats)
        {
            if (!TryMap(advertisedFormat, out _))
            {
                continue;
            }

            if (!hasSelected || ShouldReplaceSelectedFormat(advertisedFormat, shmFormat))
            {
                shmFormat = advertisedFormat;
                hasSelected = true;
            }
        }

        return hasSelected;
    }

    public static bool ShouldReplaceSelectedFormat(uint candidateFormat, uint selectedFormat)
    {
        if (!TryMap(candidateFormat, out var candidatePixelFormat) || !TryMap(selectedFormat, out var selectedPixelFormat))
        {
            return false;
        }

        return (selectedPixelFormat, candidatePixelFormat) is
            (ScreenPixelFormat.Bgra8888, ScreenPixelFormat.Xrgb8888) or
            (ScreenPixelFormat.Xbgr8888, ScreenPixelFormat.Abgr8888);
    }

    public static string FormatAdvertisedFormats(ReadOnlySpan<uint> advertisedFormats)
    {
        if (advertisedFormats.Length is 0)
        {
            return "[]";
        }

        var builder = new StringBuilder((advertisedFormats.Length * 11) + 2);
        _ = builder.Append('[');

        for (var index = 0; index < advertisedFormats.Length; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(',');
            }

            _ = builder.Append("0x");
            _ = builder.Append(advertisedFormats[index].ToString("x8", CultureInfo.InvariantCulture));
        }

        _ = builder.Append(']');
        return builder.ToString();
    }
}
