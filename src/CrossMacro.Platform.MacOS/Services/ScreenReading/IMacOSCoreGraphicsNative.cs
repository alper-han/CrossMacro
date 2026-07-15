
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSCoreGraphicsNative
{
    uint GetActiveDisplayCount();

    uint[] GetActiveDisplays(uint count);

    uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect);

    CoreGraphics.CGRect GetDisplayBounds(uint display);

    MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect);
}
