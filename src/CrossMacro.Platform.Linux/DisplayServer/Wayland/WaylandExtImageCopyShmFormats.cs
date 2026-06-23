using System.Globalization;
using System.Text;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class WaylandExtImageCopyShmFormats
{
    public const uint Argb8888 = 0x00000000;
    public const uint Xrgb8888 = 0x00000001;
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
        if (advertisedFormats.Length == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder(advertisedFormats.Length * 11 + 2);
        builder.Append('[');

        for (var index = 0; index < advertisedFormats.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("0x");
            builder.Append(advertisedFormats[index].ToString("x8", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
}
