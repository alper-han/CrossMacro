
namespace CrossMacro.Platform.MacOS.Tests.Native;

public sealed class CoreGraphicsConstantsTests
{
    [Fact]
    public void CGEventTapOptions_ListenOnly_MatchesNativeValue()
    {
        Assert.Equal(1u, (uint)CoreGraphics.CGEventTapOptions.ListenOnly);
    }

    [Fact]
    public void ScreenCaptureConstants_MatchCoreGraphicsValues()
    {
        Assert.Equal(0, (int)CoreGraphics.CGError.Success);
        Assert.Equal(0x1Fu, CoreGraphics.kCGBitmapAlphaInfoMask);
        Assert.Equal(0x7000u, CoreGraphics.kCGBitmapByteOrderMask);
        Assert.Equal(0x2000u, CoreGraphics.kCGBitmapByteOrder32Little);
        Assert.Equal(0x4000u, CoreGraphics.kCGBitmapByteOrder32Big);
    }
}
