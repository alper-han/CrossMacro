using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal static class ScreenReadingFrameFixtures
{
    public static byte[] TwoPixelXrgbBytes() =>
    [
        0x33, 0x22, 0x11, 0x00,
        0x66, 0x55, 0x44, 0x00
    ];

    public static byte[] ThreeByTwoXrgbBytes() =>
    [
        0x03, 0x02, 0x01, 0x00, 0x06, 0x05, 0x04, 0x00, 0x09, 0x08, 0x07, 0x00,
        0x0C, 0x0B, 0x0A, 0x00, 0x0F, 0x0E, 0x0D, 0x00, 0x12, 0x11, 0x10, 0x00
    ];

    public static ExtImageCopyFrame ExtFrame(ScreenRect bounds, byte[] pixels, CountingDisposable? owner = null) =>
        new(bounds, checked(bounds.Width * 4), ScreenPixelFormat.Xrgb8888, pixels, owner);

    public static WlrScreencopyFrame WlrFrame(ScreenRect bounds, byte[] pixels, CountingDisposable? owner = null) =>
        new(bounds, checked(bounds.Width * 4), ScreenPixelFormat.Xrgb8888, pixels, owner);

    public static KWinScreenShotFrame KWinFrame(ScreenRect bounds, byte[] pixels, CountingDisposable? owner = null) =>
        new(bounds, checked(bounds.Width * 4), ScreenPixelFormat.Bgra8888, pixels, owner);

    public static PortalPipeWireFrame PortalFrame(ScreenRect bounds, byte[] pixels, CountingDisposable? owner = null) =>
        new(bounds, checked(bounds.Width * 4), ScreenPixelFormat.Xrgb8888, pixels, owner);
}
