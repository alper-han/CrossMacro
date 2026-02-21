namespace CrossMacro.UI.Tests;

using Avalonia.Controls;
using CrossMacro.UI;
using CrossMacro.UI.ViewModels;

public class ViewLocatorTests
{
    [Fact]
    public void Match_WhenGivenViewModelBase_ReturnsTrue()
    {
        var locator = new ViewLocator();

        var result = locator.Match(new DummyViewModel());

        Assert.True(result);
    }

    [Fact]
    public void Match_WhenGivenNonViewModel_ReturnsFalse()
    {
        var locator = new ViewLocator();

        var result = locator.Match(new object());

        Assert.False(result);
    }

    [Fact]
    public void Build_WhenGivenUnknownType_ReturnsNotFoundTextBlock()
    {
        var locator = new ViewLocator();

        var control = locator.Build(new object());

        var textBlock = Assert.IsType<TextBlock>(control);
        Assert.StartsWith("Not Found:", textBlock.Text);
    }

    private sealed class DummyViewModel : ViewModelBase
    {
    }
}
