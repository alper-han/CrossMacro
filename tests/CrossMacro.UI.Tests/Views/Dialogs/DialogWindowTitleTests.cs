using CrossMacro.UI.Tests.Services;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Tests.Views.Dialogs;

[Collection(TestCollections.HeadlessStartup)]
public sealed class DialogWindowTitleTests
{
    [Fact]
    public Task ConfirmationDialog_UsesTheProvidedTitleForTheWindow() =>
        HeadlessTestRunner.RunAsync(_ =>
        {
            var dialog = new ConfirmationDialog("Löschen?", "Message", "Yes", "No");

            Assert.Equal("Löschen?", dialog.Title);
            Assert.Equal("Löschen?", dialog.TitleText.Text);
            return Task.CompletedTask;
        });

    [Fact]
    public Task FastLoopWarningDialog_UsesTheProvidedTitleForTheWindow() =>
        HeadlessTestRunner.RunAsync(_ =>
        {
            var dialog = new FastLoopWarningDialog("Schnelle Schleife", "Message", "Continue", "Cancel", "Do not warn");

            Assert.Equal("Schnelle Schleife", dialog.Title);
            Assert.Equal("Schnelle Schleife", dialog.TitleText.Text);
            return Task.CompletedTask;
        });
}
