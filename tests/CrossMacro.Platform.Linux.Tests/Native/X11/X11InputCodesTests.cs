namespace CrossMacro.Platform.Linux.Tests.Native.X11;

public sealed class X11InputCodesTests
{
    [Theory]
    [InlineData(1, UInputNative.BTN_LEFT)]
    [InlineData(2, UInputNative.BTN_MIDDLE)]
    [InlineData(3, UInputNative.BTN_RIGHT)]
    [InlineData(8, UInputNative.BTN_SIDE)]
    [InlineData(9, UInputNative.BTN_EXTRA)]
    public void Buttons_MapBothWaysThroughOneTable(int x11Button, int linuxButton)
    {
        Assert.Equal(linuxButton, X11InputCodes.ToLinuxButton(x11Button));
        Assert.True(X11InputCodes.TryToX11Button(linuxButton, out var back));
        Assert.Equal((uint)x11Button, back);
    }

    [Fact]
    public void UnknownButtons_PassThroughOnCaptureAndAreRejectedOnSimulation()
    {
        Assert.Equal(15, X11InputCodes.ToLinuxButton(15));
        Assert.False(X11InputCodes.TryToX11Button(UInputNative.BTN_FORWARD, out var x11Button));
        Assert.Equal(0u, x11Button);
    }

    [Theory]
    [InlineData(1, false, 4u)]
    [InlineData(-1, false, 5u)]
    [InlineData(1, true, 7u)]
    [InlineData(-1, true, 6u)]
    public void ScrollButton_FollowsDirectionAndAxis(int delta, bool horizontal, uint expected)
    {
        Assert.Equal(expected, X11InputCodes.ScrollButton(delta, horizontal));
    }

    [Fact]
    public void Keycodes_AreOffsetByEight()
    {
        Assert.Equal(38u, X11InputCodes.ToX11Keycode(30));
        Assert.Equal(30, X11InputCodes.ToLinuxKeyCode(38));
    }
}
