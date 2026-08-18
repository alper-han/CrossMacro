namespace CrossMacro.UI.Tests.Views;

public sealed class MainWindowTests
{
    [Fact]
    public void RefreshContentLayout_InvalidatesAnAlreadyArrangedContentControl()
    {
        var content = new ContentControl();
        content.Measure(new Size(100, 100));
        content.Arrange(new Rect(0, 0, 100, 100));

        Assert.True(content.IsMeasureValid);
        Assert.True(content.IsArrangeValid);

        CrossMacro.UI.Views.MainWindow.RefreshContentLayout(content);

        Assert.False(content.IsMeasureValid);
        Assert.False(content.IsArrangeValid);
    }
}
