using System.IO;
using Avalonia.Controls;
using Avalonia.Platform;
using CrossMacro.UI.Services.Desktop;
using SkiaSharp;
using Tmds.DBus.Protocol;

namespace CrossMacro.UI.Linux.Services;

/// <summary>
/// Native AOT compatible Linux system tray backend using StatusNotifierItem (SNI) and DBusMenu.
/// Registers cleanly inside Flatpak sandboxes without requiring broad D-Bus bus name ownership.
/// </summary>
internal sealed class LinuxStatusNotifierTrayBackend(string? sessionBusAddress = null) : ITrayIconBackend
{
    private readonly string? _sessionBusAddress = sessionBusAddress ?? DBusAddress.Session;

    public ITrayIconHandle Create(WindowIcon icon, NativeMenu menu, string toolTipText)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(menu);
        return new LinuxStatusNotifierTrayHandle(_sessionBusAddress, menu, toolTipText, LoadIconPixmap(icon));
    }
    private static (int Width, int Height, byte[] Pixmap) LoadIconPixmap(WindowIcon icon)
    {
        using var stream = new MemoryStream();
        icon.Save(stream);
        stream.Position = 0;
        using var sourceBitmap = SKBitmap.Decode(stream)
            ?? throw new InvalidDataException("Tray icon could not be decoded.");

        const int targetSize = 32;
        using var bitmap = (sourceBitmap.Width > targetSize || sourceBitmap.Height > targetSize)
            ? sourceBitmap.Resize(new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul), SKSamplingOptions.Default)
            : null;
        var effectiveBitmap = bitmap ?? sourceBitmap;

        var bytes = new byte[checked(effectiveBitmap.Width * effectiveBitmap.Height * 4)];
        var offset = 0;
        for (var y = 0; y < effectiveBitmap.Height; y++)
        {
            for (var x = 0; x < effectiveBitmap.Width; x++)
            {
                var color = effectiveBitmap.GetPixel(x, y);
                bytes[offset++] = color.Alpha;
                bytes[offset++] = color.Red;
                bytes[offset++] = color.Green;
                bytes[offset++] = color.Blue;
            }
        }
        return (effectiveBitmap.Width, effectiveBitmap.Height, bytes);
    }
}
