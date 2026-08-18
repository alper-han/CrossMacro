namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsWindowPolicyTests
{
    [Theory]
    [InlineData("123", 123L, true)]
    [InlineData("-42", -42L, true)]
    [InlineData("0", 0L, false)]
    [InlineData("not-a-window", 0L, false)]
    public void WindowAddressParser_UsesStableHandleContract(string address, long expectedHandle, bool expected)
    {
        var result = WindowsWindowAddressParser.TryParse(address, out var hwnd);

        Assert.Equal(expected, result);
        Assert.Equal(new IntPtr(expectedHandle), hwnd);
    }

    [Fact]
    public void WindowPlacement_ReportsVisibleMarginsAndSize()
    {
        var placement = new WindowsWindowPlacement(
            new IntPtr(42),
            new RectStruct { left = 90, top = 180, right = 1130, bottom = 880 },
            new RectStruct { left = 100, top = 200, right = 1100, bottom = 800 });

        Assert.Equal(10, placement.LeftMargin);
        Assert.Equal(20, placement.TopMargin);
        Assert.Equal(30, placement.RightMargin);
        Assert.Equal(80, placement.BottomMargin);
        Assert.Equal(40, placement.HorizontalMargin);
        Assert.Equal(100, placement.VerticalMargin);
        Assert.Equal(1000, placement.VisibleWidth);
        Assert.Equal(600, placement.VisibleHeight);
    }
}
