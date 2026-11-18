using Avalonia.VisualTree;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Tests.Views.Tabs;

[Collection(TestCollections.HeadlessStartup)]
public sealed class CancelCaptureButtonTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public Task IconOnly_ControlsWhetherTheLabelIsShown(bool iconOnly, bool labelVisible) =>
        HeadlessTestRunner.RunAsync(async token =>
        {
            var button = new CancelCaptureButton { IconOnly = iconOnly };
            var window = new Window { Content = button };
            window.Show();

            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });

                var inner = button.GetVisualDescendants().OfType<Button>().Single();
                var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
                _ = inner.Classes.Should().Contain("danger");
                Assert.Equal(labelVisible, label.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
}
