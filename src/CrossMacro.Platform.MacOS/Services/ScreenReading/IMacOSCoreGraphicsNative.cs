
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSCoreGraphicsNative
{
    public uint GetActiveDisplayCount();

    public uint[] GetActiveDisplays(uint count);

    public uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect);

    public CoreGraphics.CGRect GetDisplayBounds(uint display);

    public MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect);
}
